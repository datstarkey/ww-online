using Serilog;
using WWOnline.Hubs;
using WWOnline.Patcher.WorldData;

namespace WWOnline.Services;

/// <summary>One dungeon on the Room page's Dungeons card.</summary>
/// <param name="Keys">The small keys shown: the derived count while shared world is on, the game's own otherwise.</param>
/// <param name="KeysTaken">Key sources whose flag is set, of <paramref name="KeysTotal"/> (null without a key table).</param>
/// <param name="DungeonItem">dSv_memBit_c::mDungeonItem: map, compass, boss key, boss beaten (bits 0-3).</param>
public sealed record DungeonProgress(int Slot, int Keys, int? KeysTaken, int? KeysTotal, int? DoorsOpened, int? DoorsTotal, byte DungeonItem);

/// <summary>What the Dungeons card shows.</summary>
/// <param name="HasGame">A game is attached and in play.</param>
/// <param name="Shared">Shared world is on and the key table is built: keys are the room's derived count.</param>
public sealed record DungeonKeysSnapshot(bool HasGame, bool Shared, IReadOnlyDictionary<int, DungeonProgress> Dungeons)
{
    public static DungeonKeysSnapshot None { get; } = new(false, false, new Dictionary<int, DungeonProgress>());
}

/// <summary>The Dungeons card's data source (the service, or a fake in tests).</summary>
public interface IDungeonKeysSource
{
    DungeonKeysSnapshot Snapshot { get; }

    /// <summary>Raised on a background thread when <see cref="Snapshot"/> changes.</summary>
    event Action? SnapshotChanged;
}

/// <summary>
/// Shared small keys. With Shared world on, every dungeon's small-key count is derived from the world
/// flags every player shares (keys taken - key doors opened, <see cref="SmallKeyReconciler"/>) and written
/// into the game at 4 Hz, only in a settled scene. Off (or not in a room), the game's own counts are left alone. There is no
/// server state and nothing to send: the flags already sync through <see cref="WorldFlagSyncService"/>.
/// Also publishes each dungeon's keys and items for the Room page.
/// </summary>
public sealed class SharedSmallKeyService : IDungeonKeysSource, IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<SharedSmallKeyService>();
    private const int TickMs = 250;

    private readonly IDolphinService _dolphin;
    private readonly SignalRClientService _signalR;
    private readonly RoomSettingsService _room;
    private readonly SmallKeyTableProvider _tables;
    private readonly SmallKeyReconciler _reconciler = new();
    private readonly SceneStabilityGate _scene = new(requiredTicks: 4);
    private readonly object _tickLock = new();
    private System.Timers.Timer? _timer;
    private long _tick;
    private bool _wasSharing;

    private DungeonKeysSnapshot _snapshot = DungeonKeysSnapshot.None;

    public SharedSmallKeyService(IDolphinService dolphin, SignalRClientService signalR, RoomSettingsService room, SmallKeyTableProvider tables)
    {
        _dolphin = dolphin;
        _signalR = signalR;
        _room = room;
        _tables = tables;
    }

    public DungeonKeysSnapshot Snapshot => Volatile.Read(ref _snapshot);

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
            catch (Exception ex) { Logger.Error(ex, "[keys] tick failed"); }
            finally { Monitor.Exit(_tickLock); }
        };
        _timer.Start();
        Logger.Information("[keys] shared small keys started");
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
            _wasSharing = false;
        }
        Publish(DungeonKeysSnapshot.None);
    }

    private void Tick()
    {
        _tick++;
        if (!_dolphin.IsConnected || !_scene.Check(_dolphin) || SmallKeyMemory.Read(_dolphin) is not { } obs)
        {
            _reconciler.Reset();
            // Keep showing the last dungeons through a stage change; clear them without a game in play.
            if (!_dolphin.IsConnected || _scene.Stage.Length == 0 || WWOnline.Data.StageIDs.IsNonGameplayStage(_scene.Stage))
                Publish(DungeonKeysSnapshot.None);
            return;
        }

        var table = _tables.Table;
        // In a room with Shared world on. Offline (or the rule off) every game keeps its own counts.
        bool sharing = _signalR.IsConnected && _room.Current.SharedWorld && table != null;
        if (sharing != _wasSharing)
        {
            _wasSharing = sharing;
            _reconciler.Reset();
            Logger.Information(sharing
                ? "[keys] shared small keys on: each dungeon's count is derived from the shared world's flags"
                : "[keys] shared small keys off: every game keeps its own counts");
        }

        if (sharing)
        {
            var log = new List<string>();
            foreach (var write in _reconciler.Step(table!, obs, _tick, log))
            {
                    if (!SmallKeyMemory.Apply(_dolphin, write))
                    Logger.Warning("[keys] write for slot {Slot} ({Kind} {Value}) didn't go through; retrying", write.Slot, write.Kind, write.Value);
            }
            foreach (var line in log) Logger.Information("[keys] {Line}", line);
        }
        Publish(BuildSnapshot(table, obs, sharing));
    }

    /// <summary>The Dungeons card's view of an observation (after this tick's writes, so it shows the target).</summary>
    private DungeonKeysSnapshot BuildSnapshot(SmallKeyTable? table, SmallKeyObservation obs, bool sharing)
    {
        var dungeons = new Dictionary<int, DungeonProgress>();
        for (int slot = 0; slot < obs.Slots.Length; slot++)
        {
            var s = obs.Slots[slot];
            var d = table?.For(slot);
            if (d is { Doors.Count: > 0 })
            {
                int keys = sharing ? _reconciler.Target(d, s) : s.KeyNum;
                dungeons[slot] = new DungeonProgress(slot, keys, d.Taken(s.Tbox, s.Item), d.Sources.Count, d.Opened(s.Switch), d.Doors.Count, s.DungeonItem);
            }
            else
            {
                dungeons[slot] = new DungeonProgress(slot, s.KeyNum, null, null, null, null, s.DungeonItem);
            }
        }
        return new DungeonKeysSnapshot(true, sharing, dungeons);
    }

    private void Publish(DungeonKeysSnapshot snapshot)
    {
        var old = Volatile.Read(ref _snapshot);
        if (Same(old, snapshot)) return;
        Volatile.Write(ref _snapshot, snapshot);
        SnapshotChanged?.Invoke();
    }

    private static bool Same(DungeonKeysSnapshot a, DungeonKeysSnapshot b) =>
        a.HasGame == b.HasGame && a.Shared == b.Shared && a.Dungeons.Count == b.Dungeons.Count &&
        a.Dungeons.All(kv => b.Dungeons.TryGetValue(kv.Key, out var o) && o == kv.Value);

    public void Dispose() => Stop();
}
