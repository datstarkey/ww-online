using Serilog;
using WWOnline.Data;
using WWOnline.Hubs;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>
/// A shared bag (bait: <see cref="SharedBaitService"/>, spoils: <see cref="SharedSpoilsService"/>), modelled on
/// the shared wallet. The server holds one room total per item type (<typeparamref name="T"/>); each client:
///   - once its game has the bag, joins with its bag's counts (the room owner's bag seeds them) and adopts the
///     room's,
///   - sends every local change as a signed per-type delta,
///   - writes each total the server pushes into its own bag (<see cref="ApplyTarget"/>), only while no event
///     runs and the menu is closed.
/// Local changes go first, so one that races a pushed total isn't overwritten unsent. Resets and rejoins on
/// every new connection and whenever the rule is turned back on; a send that fails is put back and resent.
/// </summary>
public abstract class SharedBagService<T> : IDisposable where T : class, IBagCounts<T>
{
    private const int TickMs = 250;
    private static readonly TimeSpan JoinRetry = TimeSpan.FromSeconds(3);

    protected readonly ILogger Logger;
    protected readonly IDolphinService Dolphin;
    protected readonly SignalRClientService SignalR;
    private readonly RoomSettingsService _room;
    private readonly object _tickLock = new();
    private readonly SceneStabilityGate _scene = new(requiredTicks: 4); // 1s at 4Hz
    private System.Timers.Timer? _timer;
    private bool _attached;

    // Guarded by _tickLock
    private bool _joined;
    private DateTime _nextJoinAttempt;
    private bool _loggedWaiting;
    private bool _loggedNoBag;
    private int _connectionGeneration;
    private T? _baseline; // the bag's counts as of our last sync point
    private T? _target;   // latest total pushed by the server, not yet applied

    private int _joining;

    /// <summary>The room's counts as last reported by the server (null until joined).</summary>
    public T? Total { get; private set; }

    /// <summary>Raised (on a background thread) whenever <see cref="Total"/> changes.</summary>
    public event Action<T?>? TotalChanged;

    protected SharedBagService(IDolphinService dolphin, SignalRClientService signalR, RoomSettingsService room)
    {
        Logger = Log.ForContext(GetType());
        Dolphin = dolphin;
        SignalR = signalR;
        _room = room;
    }

    // ── What differs per bag ─────────────────────────────────────────────────

    /// <summary>Log tag without brackets: "bait", "spoils".</summary>
    protected abstract string Tag { get; }

    /// <summary>For the log: "shared bait bag".</summary>
    protected abstract string What { get; }

    protected abstract bool RuleOn(RoomSettings rules);

    /// <summary>The game has this bag (its inventory slot holds it).</summary>
    protected abstract bool OwnsBag();

    /// <summary>The bag's counts now, or null when they can't be read or the game is still applying a change.
    /// Also where a subclass remembers the snapshot <see cref="ApplyTarget"/> writes over.</summary>
    protected abstract T? ReadCounts();

    /// <summary>
    /// Called with every readable <paramref name="current"/> while joined, before local changes are
    /// compared: a new sync point once an earlier <see cref="ApplyTarget"/> has settled (e.g. the game applied a
    /// pending count), else null.
    /// </summary>
    protected virtual T? Settle(T current) => null;

    /// <summary>
    /// Write <paramref name="target"/> into the bag read as <paramref name="current"/> (the game is idle).
    /// Applied: the target is done (clear it); Baseline: the new sync point, if it moves now.
    /// </summary>
    protected abstract (bool Applied, T? Baseline) ApplyTarget(T current, T target);

    /// <summary>Forget per-bag state when the sync resets (new connection, rule off, stop).</summary>
    protected virtual void OnReset() { }

    protected abstract void Subscribe(Action<T> onTotal);
    protected abstract void Unsubscribe(Action<T> onTotal);
    protected abstract Task<T?> JoinAsync(T current);
    protected abstract Task<bool> SendDeltaAsync(T delta);

    /// <summary>No event running (shops, trades, feeding, cutscenes) and the pause menu is closed: safe to rewrite a bag.</summary>
    public static bool IsIdle(IDolphinService dolphin) =>
        dolphin.ReadMemory(GameMemoryAddresses.Events.EventMode, 1) is [0] &&
        dolphin.ReadMemory(GameMemoryAddresses.Events.MenuPause, 1) is [0];

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void SetTotal(T? total)
    {
        if (Equals(Total, total)) return;
        Total = total;
        TotalChanged?.Invoke(total);
    }

    public void Start()
    {
        if (_timer != null) return;
        Attach();

        _timer = new System.Timers.Timer(TickMs) { AutoReset = true };
        _timer.Elapsed += (_, _) =>
        {
            if (!Monitor.TryEnter(_tickLock)) return;
            try { TickLocked(); }
            catch (Exception ex) { Logger.Error(ex, "[{Tag:l}] tick failed", Tag); }
            finally { Monitor.Exit(_tickLock); }
        };
        _timer.Start();
        Logger.Information("[{Tag:l}] {What:l} sync started", Tag, What);
    }

    public void Stop()
    {
        if (!_attached) return;
        _attached = false;
        Unsubscribe(OnTotalReceived);
        SignalR.Connected -= OnConnected;
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
        lock (_tickLock) ResetSync();
        SetTotal(null);
    }

    /// <summary>Subscribe to the hub callbacks without starting the timer (tests drive <see cref="Tick"/>).</summary>
    public void Attach()
    {
        if (_attached) return;
        _attached = true;
        Subscribe(OnTotalReceived);
        SignalR.Connected += OnConnected;
    }

    /// <summary>One sync step. The timer runs it every 250 ms; tests call it directly (without <see cref="Start"/>'s
    /// timer, after <see cref="Attach"/>).</summary>
    public void Tick()
    {
        lock (_tickLock) TickLocked();
    }

    /// <summary>Leave the bag; we rejoin (and re-adopt the server's counts) on the next tick.</summary>
    private void ResetSync()
    {
        _joined = false;
        _nextJoinAttempt = DateTime.MinValue;
        _loggedWaiting = false;
        _baseline = null;
        _target = null;
        OnReset();
    }

    private void OnTotalReceived(T total)
    {
        if (total == null || !total.IsValidTotal()) return; // never from our server; don't write garbage into the bag
        lock (_tickLock)
        {
            _target = total.Clone();
            if (!_joined) _nextJoinAttempt = DateTime.MinValue; // bag just got seeded? join now
        }
        SetTotal(total.Clone());
    }

    // New connection (the server may have restarted with an empty bag): rejoin.
    private void OnConnected()
    {
        lock (_tickLock)
        {
            _connectionGeneration++;
            ResetSync();
        }
        SetTotal(null);
    }

    private void TickLocked()
    {
        // Offline: nothing to sync (Connected rejoins).
        if (!Dolphin.IsConnected || !SignalR.IsConnected) return;

        // Room rule off: keep our own bag; rejoin (and re-adopt the counts) when it's back on.
        if (!RuleOn(_room.Current))
        {
            if (_joined)
            {
                ResetSync();
                SetTotal(null);
                Logger.Information("[{Tag:l}] {What:l} turned off by the room owner", Tag, What);
            }
            return;
        }

        // In gameplay with a settled scene (the title demo runs on an empty default save).
        if (!_scene.Check(Dolphin)) return;

        // No bag yet: nothing to share (shared items brings the bag itself when someone gets it).
        if (!OwnsBag())
        {
            if (!_loggedNoBag)
            {
                _loggedNoBag = true;
                Logger.Information("[{Tag:l}] this game has no {Tag:l} bag yet: joining the {What:l} once it has one", Tag, Tag, What);
            }
            return;
        }
        _loggedNoBag = false;

        if (ReadCounts() is not { } current) return;

        if (!_joined)
        {
            // A bag the game can't make: don't seed the room with it.
            if (current.IsValidTotal()) TryJoin(current);
            return;
        }

        if (Settle(current) is { } settled) _baseline = settled;

        // Local change first, so one that races a pushed total isn't overwritten unsent.
        if (_baseline is { } b && !current.Equals(b))
        {
            SendDelta(current.Minus(b));
            _baseline = current;
            return; // the server's resulting total arrives as _target shortly
        }

        if (_target is not { } t) return;
        if (current.Equals(t))
        {
            _target = null;
            return;
        }
        // Rewriting a bag mid-event (a shop, a trade, feeding) or with the menu open (it draws the bag from
        // its own copy) could fight the game: keep the target until it's idle.
        if (!IsIdle(Dolphin)) return;

        var (applied, baseline) = ApplyTarget(current, t);
        if (applied) _target = null;
        if (baseline != null) _baseline = baseline;
    }

    /// <summary>Send a local change; if it wasn't delivered, put it back so the next tick resends it.</summary>
    private void SendDelta(T delta)
    {
        int generation = _connectionGeneration;
        Logger.Information("[{Tag:l}] local {Delta} → sending", Tag, delta.DeltaText());
        _ = Task.Run(async () =>
        {
            bool sent = false;
            try { sent = await SendDeltaAsync(delta); }
            catch (Exception ex) { Logger.Warning(ex, "[{Tag:l}] send failed ({Delta})", Tag, delta.DeltaText()); }
            if (sent) return;
            lock (_tickLock)
            {
                // Same connection: undo the baseline move so the difference is sent again.
                // (A new connection rejoins and adopts the server's counts instead.)
                if (generation == _connectionGeneration && _joined && _baseline is { } b)
                    _baseline = b.Minus(delta);
            }
        });
    }

    private void TryJoin(T current)
    {
        if (DateTime.UtcNow < _nextJoinAttempt) return;
        if (Interlocked.Exchange(ref _joining, 1) == 1) return;
        _nextJoinAttempt = DateTime.UtcNow + JoinRetry;
        int generation = _connectionGeneration;
        var joinWith = current.Clone();

        _ = Task.Run(async () =>
        {
            try
            {
                var total = await JoinAsync(joinWith);
                if (total == null || !total.IsValidTotal())
                {
                    bool log;
                    lock (_tickLock)
                    {
                        log = total == null && !_loggedWaiting && SignalR.IsConnected && RuleOn(_room.Current);
                        if (log) _loggedWaiting = true;
                    }
                    if (log)
                        Logger.Information("[{Tag:l}] {What:l} not ready — waiting for room owner {Owner} to seed it (retrying)",
                            Tag, What, _room.Current.OwnerName);
                    return;
                }
                lock (_tickLock)
                {
                    if (generation != _connectionGeneration) return; // reconnected meanwhile: join again
                    _baseline = joinWith;
                    _target = total;
                    _joined = true;
                    _loggedWaiting = false;
                }
                SetTotal(total.Clone());
                Logger.Information("[{Tag:l}] joined {What:l}: {Total} (had {Current})", Tag, What, total, joinWith);
            }
            catch (Exception ex) { Logger.Warning(ex, "[{Tag:l}] join failed (retrying)", Tag); }
            finally { Interlocked.Exchange(ref _joining, 0); }
        });
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
