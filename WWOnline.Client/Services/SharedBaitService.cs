using Serilog;
using WWOnline.Hubs;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>
/// Shared bait bag (room rule <see cref="RoomSettings.SharedBait"/>), modelled on the shared wallet. The
/// server holds one All-Purpose Bait count and one Hyoi Pear count (<see cref="BaitCounts"/>); each client:
///   - once its game has the bait bag, joins with its bag's counts (the room owner's bag seeds them) and
///     adopts the room's,
///   - sends every local change (bait used, bought or picked up) as a per-type delta,
///   - writes each total the server pushes into its own bag (<see cref="BaitBag.Apply"/>: fewest slot
///     changes, the way the game stacks them), only while no event runs and the menu is closed.
/// The write is compare-and-swap: the bag is re-read just before writing and nothing is written if the game
/// changed it since (the local change is then sent first, and the server's next total applied after). The
/// sync point after a write is what was written, so a use the game makes right after is still a local change.
/// </summary>
public class SharedBaitService : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<SharedBaitService>();
    private const int TickMs = 250;
    private static readonly TimeSpan JoinRetry = TimeSpan.FromSeconds(3);

    private readonly IDolphinService _dolphin;
    private readonly SignalRClientService _signalR;
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
    private BaitCounts? _baseline; // the bag's counts as of our last sync point
    private BaitCounts? _target;   // latest total pushed by the server, not yet applied

    private int _joining;

    /// <summary>The room's bait counts as last reported by the server (null until joined).</summary>
    public BaitCounts? Total { get; private set; }

    /// <summary>Raised (on a background thread) whenever <see cref="Total"/> changes.</summary>
    public event Action<BaitCounts?>? TotalChanged;

    private void SetTotal(BaitCounts? total)
    {
        if (Equals(Total, total)) return;
        Total = total;
        TotalChanged?.Invoke(total);
    }

    public SharedBaitService(IDolphinService dolphin, SignalRClientService signalR, RoomSettingsService room)
    {
        _dolphin = dolphin;
        _signalR = signalR;
        _room = room;
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
            catch (Exception ex) { Logger.Error(ex, "[bait] tick failed"); }
            finally { Monitor.Exit(_tickLock); }
        };
        _timer.Start();
        Logger.Information("[bait] shared bait bag sync started");
    }

    public void Stop()
    {
        if (!_attached) return;
        _attached = false;
        _signalR.BaitTotalReceived -= OnBaitTotalReceived;
        _signalR.Connected -= OnConnected;
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
        lock (_tickLock) ResetSync();
        SetTotal(null);
    }

    /// <summary>Leave the bag; we rejoin (and re-adopt the server's counts) on the next tick.</summary>
    private void ResetSync()
    {
        _joined = false;
        _nextJoinAttempt = DateTime.MinValue;
        _loggedWaiting = false;
        _baseline = null;
        _target = null;
    }

    private void OnBaitTotalReceived(BaitCounts total)
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

    /// <summary>One sync step. The timer runs it every 250 ms; tests call it directly (without <see cref="Start"/>'s
    /// timer, after <see cref="Attach"/>).</summary>
    public void Tick()
    {
        lock (_tickLock) TickLocked();
    }

    /// <summary>Subscribe to the hub callbacks without starting the timer (tests drive <see cref="Tick"/>).</summary>
    public void Attach()
    {
        if (_attached) return;
        _attached = true;
        _signalR.BaitTotalReceived += OnBaitTotalReceived;
        _signalR.Connected += OnConnected;
    }

    private void TickLocked()
    {
        // Offline: nothing to sync (Connected rejoins).
        if (!_dolphin.IsConnected || !_signalR.IsConnected) return;

        // Room rule off: keep our own bag; rejoin (and re-adopt the counts) when it's back on.
        if (!_room.Current.SharedBait)
        {
            if (_joined)
            {
                ResetSync();
                SetTotal(null);
                Logger.Information("[bait] shared bait bag turned off by the room owner");
            }
            return;
        }

        // In gameplay with a settled scene (the title demo runs on an empty default save).
        if (!_scene.Check(_dolphin)) return;

        // No bag yet: nothing to share (shared items brings the bag itself when someone gets it).
        if (!BaitBagMemory.OwnsBag(_dolphin))
        {
            if (!_loggedNoBag)
            {
                _loggedNoBag = true;
                Logger.Information("[bait] no bait bag yet: joining the shared bait bag once this game has one");
            }
            return;
        }
        _loggedNoBag = false;

        if (BaitBagMemory.Read(_dolphin) is not { } bag) return;
        var current = bag.Count();

        if (!_joined)
        {
            // A bag the game can't make (more than 8 slots' worth): don't seed the room with it.
            if (current.IsValidTotal()) TryJoin(current);
            return;
        }

        // Local change first, so a use that races a pushed total isn't overwritten unsent.
        if (_baseline is { } b && !current.Equals(b))
        {
            SendDelta(current - b);
            _baseline = current;
            return; // the server's resulting total arrives as _target shortly
        }

        if (_target is not { } t) return;
        if (current.Equals(t))
        {
            _target = null;
            return;
        }
        // Rewriting the bag mid-event (a shop, feeding a fish or a seagull) or with the menu open (it draws
        // the bag from its own copy) could fight the game: keep the target until it's idle.
        if (!BaitBagMemory.IsIdle(_dolphin)) return;

        int equipped = BaitBag.EquippedMask(BaitBagMemory.ReadSelectSlots(_dolphin) ?? []);
        var next = BaitBag.Apply(bag, t, equipped);
        switch (BaitBagMemory.Write(_dolphin, bag, next))
        {
            case BaitBagMemory.WriteResult.Written:
                _target = null;
                _baseline = next.Count();
                if (_baseline.Equals(t))
                    Logger.Information("[bait] shared bag {Total} → applied ({Delta}): {Slots}", t, (t - current).DeltaText(), next);
                else
                    Logger.Warning("[bait] shared bag {Total}: only {Applied} fits this bag ({Slots})", t, _baseline, next);
                break;
            case BaitBagMemory.WriteResult.Raced:
                // The game changed the bag since we read it: the next tick sends that change, then applies.
                break;
            case BaitBagMemory.WriteResult.Failed:
                // Partly written, maybe: adopt what the bag now holds as the sync point (so our own
                // half-write isn't sent as a local change) and keep the target to retry.
                if (BaitBagMemory.Read(_dolphin) is { } after) _baseline = after.Count();
                Logger.Warning("[bait] writing the bag failed; retrying");
                break;
        }
    }

    /// <summary>Send a local change; if it wasn't delivered, put it back so the next tick resends it.</summary>
    private void SendDelta(BaitCounts delta)
    {
        int generation = _connectionGeneration;
        Logger.Information("[bait] local {Delta} → sending", delta.DeltaText());
        _ = Task.Run(async () =>
        {
            bool sent = false;
            try { sent = await _signalR.SendBaitDeltaAsync(delta); }
            catch (Exception ex) { Logger.Warning(ex, "[bait] send failed ({Delta})", delta.DeltaText()); }
            if (sent) return;
            lock (_tickLock)
            {
                // Same connection: undo the baseline move so the difference is sent again.
                // (A new connection rejoins and adopts the server's counts instead.)
                if (generation == _connectionGeneration && _joined && _baseline is { } b)
                    _baseline = b - delta;
            }
        });
    }

    private void TryJoin(BaitCounts current)
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
                var total = await _signalR.JoinBaitAsync(joinWith);
                if (total == null || !total.IsValidTotal())
                {
                    bool log;
                    lock (_tickLock)
                    {
                        log = total == null && !_loggedWaiting && _signalR.IsConnected && _room.Current.SharedBait;
                        if (log) _loggedWaiting = true;
                    }
                    if (log)
                        Logger.Information("[bait] shared bait bag not ready — waiting for room owner {Owner} to seed it (retrying)",
                            _room.Current.OwnerName);
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
                Logger.Information("[bait] joined shared bait bag: {Total} (had {Current})", total, joinWith);
            }
            catch (Exception ex) { Logger.Warning(ex, "[bait] join failed (retrying)"); }
            finally { Interlocked.Exchange(ref _joining, 0); }
        });
    }

    public void Dispose() => Stop();
}
