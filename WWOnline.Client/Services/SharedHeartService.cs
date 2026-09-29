using Serilog;
using WWOnline.Hubs;
using WWOnline.Patcher.WorldData;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>What the Room items page shows about hearts.</summary>
/// <param name="HasGame">A game is attached and in play, and the heart table is built.</param>
/// <param name="Derived">Max health is derived from the room's flags (<see cref="SharedHeartService.RuleApplies"/>).</param>
/// <param name="MaxLife">The max health shown: the derived value while <see cref="Derived"/>, else the game's own.</param>
public sealed record HeartsSnapshot(bool HasGame, bool Derived, int Containers, int ContainersTotal, int Pieces, int PiecesTotal, int MaxLife)
{
    public static HeartsSnapshot None { get; } = new(false, false, 0, 0, 0, 0, 0);
}

/// <summary>The Room items page's heart data source (the service, or a fake in tests).</summary>
public interface IHeartsSource
{
    HeartsSnapshot Snapshot { get; }

    /// <summary>Raised on a background thread when <see cref="Snapshot"/> changes.</summary>
    event Action? SnapshotChanged;
}

/// <summary>Who decides mMaxLife: while this says so, the shared items leave max health out (RoomInventorySyncService).</summary>
public interface IMaxHealthOwner
{
    /// <summary>Max health is derived from the room's flags right now (connected, the rule on, the heart table built).</summary>
    bool OwnsMaxHealth { get; }

    /// <summary>The derived max health (quarter hearts) as last computed, or null while not derived / not known yet.</summary>
    int? DerivedMaxHealth { get; }
}

/// <summary>
/// Derived max health (docs/hearts.md). In a room with Shared items AND Shared world on, this game's max health is
/// <c>12 + 4 × Heart Containers + Pieces of Heart</c> whose flag is set (<see cref="HeartReconciler"/>, the flags from
/// <see cref="HeartTableProvider"/>'s table), written at 4 Hz only in a settled scene and while the game is idle, and
/// RoomInventory's max-merge leaves max health alone (<see cref="IMaxHealthOwner"/>). World flags (chests, placed items,
/// boss containers) come with Shared world; NPC reward event bits only with Shared story (else each player's own
/// count); registers, sunken-treasure / ocean salvage bits and Maggie's letter are always per player. Off (or not in a
/// room, or no heart table), the game keeps its own max health and Shared items max-merges it as before. There is no
/// server state and nothing to send: the flags already sync. Also publishes the counts for the Room items page.
/// </summary>
public sealed class SharedHeartService : IHeartsSource, IMaxHealthOwner, IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<SharedHeartService>();
    private const int TickMs = 250;

    private readonly IDolphinService _dolphin;
    private readonly SignalRClientService _signalR;
    private readonly RoomSettingsService _room;
    private readonly HeartTableProvider _tables;
    private readonly HeartReconciler _reconciler = new();
    private readonly SceneStabilityGate _scene = new(requiredTicks: 4);
    private readonly object _tickLock = new();
    private System.Timers.Timer? _timer;
    private long _tick;
    private bool _wasDeriving;

    private HeartsSnapshot _snapshot = HeartsSnapshot.None;

    public SharedHeartService(IDolphinService dolphin, SignalRClientService signalR, RoomSettingsService room, HeartTableProvider tables)
    {
        _dolphin = dolphin;
        _signalR = signalR;
        _room = room;
        _tables = tables;
    }

    /// <summary>The rooms whose max health is derived: Shared items (max health is an item) with Shared world (the flags).</summary>
    public static bool RuleApplies(RoomSettings rules) => rules.SharedItems && rules.SharedWorld;

    public bool OwnsMaxHealth => _signalR.IsConnected && RuleApplies(_room.Current) && _tables.Table != null;

    public int? DerivedMaxHealth => Snapshot is { HasGame: true, Derived: true } s ? s.MaxLife : null;

    public HeartsSnapshot Snapshot => Volatile.Read(ref _snapshot);

    public event Action? SnapshotChanged;

    public void Start()
    {
        if (_timer != null) return;
        _ = _tables.Table; // start building the table now
        _timer = new System.Timers.Timer(TickMs) { AutoReset = true };
        _timer.Elapsed += (_, _) =>
        {
            if (!Monitor.TryEnter(_tickLock)) return;
            try { Tick(); }
            catch (Exception ex) { Logger.Error(ex, "[hearts] tick failed"); }
            finally { Monitor.Exit(_tickLock); }
        };
        _timer.Start();
        Logger.Information("[hearts] derived max health started");
    }

    public void Stop()
    {
        if (_timer == null) return;
        _timer.Stop();
        _timer.Dispose();
        _timer = null;
        lock (_tickLock)
        {
            _reconciler.Reset();
            _wasDeriving = false;
        }
        Publish(HeartsSnapshot.None);
    }

    private void Tick()
    {
        _tick++;
        if (!_dolphin.IsConnected || !_scene.Check(_dolphin) || HeartMemory.Read(_dolphin) is not { } obs)
        {
            _reconciler.Unsettled();
            if (!_dolphin.IsConnected || _scene.Stage.Length == 0 || WWOnline.Data.StageIDs.IsNonGameplayStage(_scene.Stage))
                Publish(HeartsSnapshot.None);
            return;
        }

        var table = _tables.Table;
        bool deriving = _signalR.IsConnected && RuleApplies(_room.Current) && table != null; // OwnsMaxHealth, on this table
        if (deriving != _wasDeriving)
        {
            _wasDeriving = deriving;
            _reconciler.Reset();
            if (deriving)
                Logger.Information("[hearts] derived max health on: 12 + 4 per Heart Container + 1 per Piece of Heart whose flag is set " +
                                   "(Shared story {Story})", _room.Current.SharedStory ? "on" : "off: NPC rewards count per player");
            else
                Logger.Information("[hearts] derived max health off: this game keeps its own max health");
        }

        if (deriving)
        {
            var log = new List<HeartLogLine>();
            if (_reconciler.Step(table!, obs, _tick, log) is { } write && !HeartMemory.ApplyDelta(_dolphin, write.Delta))
                Logger.Warning("[hearts] max health write {From} → {To} didn't go through; retrying", write.From, write.To);
            foreach (var line in log)
            {
                if (line.Warn) Logger.Warning("[hearts] {Line}", line.Text);
                else Logger.Information("[hearts] {Line}", line.Text);
            }
        }
        Publish(BuildSnapshot(table, obs, deriving));
    }

    private static HeartsSnapshot BuildSnapshot(HeartTable? table, HeartObservation obs, bool deriving)
    {
        if (table == null) return HeartsSnapshot.None;
        var tally = table.Tally(obs.Flags);
        return new HeartsSnapshot(true, deriving, tally.Containers, table.ContainersTotal, tally.Pieces, table.PiecesTotal,
            deriving ? tally.MaxLife : obs.MaxLife);
    }

    private void Publish(HeartsSnapshot snapshot)
    {
        if (Volatile.Read(ref _snapshot) == snapshot) return;
        Volatile.Write(ref _snapshot, snapshot);
        SnapshotChanged?.Invoke();
    }

    public void Dispose() => Stop();
}
