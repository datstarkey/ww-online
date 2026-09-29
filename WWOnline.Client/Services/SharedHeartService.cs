using Serilog;
using WWOnline.Hubs;
using WWOnline.Patcher.WorldData;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>What the Room items page shows about hearts.</summary>
/// <param name="HasGame">A game is attached and in play, and the heart table is built.</param>
/// <param name="Derived">Max health is derived from the room's heart sources (<see cref="SharedHeartService.RuleApplies"/>).</param>
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

/// <summary>What RoomInventorySyncService needs from the derived hearts (<see cref="DerivedHeartsState"/>).</summary>
public interface IMaxHealthOwner
{
    /// <summary>Max health is derived from the room's heart sources right now (connected, Shared items on, the heart
    /// table built): the shared items leave MaxHealth out of the merge.</summary>
    bool OwnsMaxHealth { get; }

    /// <summary>The derived max health (quarter hearts) as last computed, or null while not derived / not known yet.</summary>
    int? DerivedMaxHealth { get; }

    /// <summary>The catalogue sources this game's own flags show taken (RoomInventory.HeartSources bits): what it adds
    /// to the room's set. 0 until the heart table is built and the game read.</summary>
    ulong LocalHeartSources { get; }
}

/// <summary>
/// The derived hearts' state shared between <see cref="SharedHeartService"/> (which writes it every tick) and
/// RoomInventorySyncService (which reads it); a separate object so neither service depends on the other twice.
/// </summary>
public sealed class DerivedHeartsState : IMaxHealthOwner
{
    private volatile bool _owns;
    private long _derived = -1;
    private long _local;

    public bool OwnsMaxHealth => _owns;
    public int? DerivedMaxHealth => Interlocked.Read(ref _derived) is var d and >= 0 ? (int)d : null;
    public ulong LocalHeartSources => (ulong)Interlocked.Read(ref _local);

    public void Update(bool owns, int? derived, ulong local)
    {
        _owns = owns;
        Interlocked.Exchange(ref _derived, derived ?? -1);
        Interlocked.Exchange(ref _local, (long)local);
    }
}

/// <summary>
/// Derived max health (docs/hearts.md). In a room with Shared items on, this game's max health is
/// <c>12 + 4 × Heart Containers + Pieces of Heart</c> taken by anyone in the room: every client adds the catalogue
/// sources its own flags show (<see cref="HeartTable.SourceBits"/>) to RoomInventory's grow-only
/// <see cref="RoomInventory.HeartSources"/>, and each source in that set (or set in this game) counts once
/// (<see cref="HeartReconciler"/>). The per-player flags themselves don't sync; only the count uses the set, so it
/// doesn't depend on Shared world or Shared story. Written at 4 Hz, only in a settled scene, while the game is idle and
/// once the room's inventory is known; RoomInventory's max-merge leaves max health alone (<see cref="IMaxHealthOwner"/>).
/// Off (or not in a room, or no heart table), the game keeps its own max health. Also publishes the counts for the
/// Room items page.
/// </summary>
public sealed class SharedHeartService : IHeartsSource, IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<SharedHeartService>();
    private const int TickMs = 250;

    private readonly IDolphinService _dolphin;
    private readonly SignalRClientService _signalR;
    private readonly RoomSettingsService _room;
    private readonly HeartTableProvider _tables;
    private readonly RoomInventorySyncService _roomInventory;
    private readonly DerivedHeartsState _state;
    private readonly HeartReconciler _reconciler = new();
    private readonly SceneStabilityGate _scene = new(requiredTicks: 4);
    private readonly object _tickLock = new();
    private System.Timers.Timer? _timer;
    private long _tick;
    private bool _wasDeriving;

    private HeartsSnapshot _snapshot = HeartsSnapshot.None;

    public SharedHeartService(IDolphinService dolphin, SignalRClientService signalR, RoomSettingsService room,
        HeartTableProvider tables, RoomInventorySyncService roomInventory, DerivedHeartsState state)
    {
        _dolphin = dolphin;
        _signalR = signalR;
        _room = room;
        _tables = tables;
        _roomInventory = roomInventory;
        _state = state;
    }

    /// <summary>The rooms whose max health is derived: Shared items (hearts are items).</summary>
    public static bool RuleApplies(RoomSettings rules) => rules.SharedItems;

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
            _state.Update(false, null, 0);
        }
        Publish(HeartsSnapshot.None);
    }

    private void Tick()
    {
        _tick++;
        var table = _tables.Table;
        bool deriving = _signalR.IsConnected && RuleApplies(_room.Current) && table != null;
        if (!_dolphin.IsConnected || !_scene.Check(_dolphin) || HeartMemory.Read(_dolphin) is not { } obs)
        {
            _reconciler.Unsettled();
            _state.Update(deriving, _state.DerivedMaxHealth, _state.LocalHeartSources); // keep the last known values
            if (!_dolphin.IsConnected || _scene.Stage.Length == 0 || WWOnline.Data.StageIDs.IsNonGameplayStage(_scene.Stage))
                Publish(HeartsSnapshot.None);
            return;
        }

        if (deriving != _wasDeriving)
        {
            _wasDeriving = deriving;
            _reconciler.Reset();
            Logger.Information(deriving
                ? "[hearts] derived max health on: 12 + 4 per Heart Container + 1 per Piece of Heart anyone in the room has taken"
                : "[hearts] derived max health off: this game keeps its own max health");
        }

        ulong local = table?.SourceBits(obs.Flags) ?? 0;
        // The room's set once the room inventory is known (joined); until then nothing is written (a reconnect clears it).
        var room = deriving ? _roomInventory.Room : null;
        ulong roomSources = room?.HeartSources ?? 0;
        var tally = table?.Tally(obs.Flags, roomSources);
        _state.Update(deriving, deriving ? tally?.MaxLife : null, local);

        if (deriving && room != null)
        {
            var log = new List<HeartLogLine>();
            if (_reconciler.Step(table!, obs, roomSources, _tick, log) is { } write && !HeartMemory.ApplyDelta(_dolphin, write.Delta))
                Logger.Warning("[hearts] max health write {From} → {To} didn't go through; retrying", write.From, write.To);
            foreach (var line in log)
            {
                if (line.Warn) Logger.Warning("[hearts] {Line}", line.Text);
                else Logger.Information("[hearts] {Line}", line.Text);
            }
        }
        else
        {
            _reconciler.Unsettled();
        }
        Publish(table == null || tally is not { } t
            ? HeartsSnapshot.None
            : new HeartsSnapshot(true, deriving, t.Containers, table.ContainersTotal, t.Pieces, table.PiecesTotal, deriving ? t.MaxLife : obs.MaxLife));
    }

    private void Publish(HeartsSnapshot snapshot)
    {
        if (Volatile.Read(ref _snapshot) == snapshot) return;
        Volatile.Write(ref _snapshot, snapshot);
        SnapshotChanged?.Invoke();
    }

    public void Dispose() => Stop();
}
