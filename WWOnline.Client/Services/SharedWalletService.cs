using Serilog;
using WWOnline.Data;
using WWOnline.Hubs;
using WWOnline.Shared.Hubs;

namespace WWOnline.Services;

/// <summary>
/// Shared rupee wallet. The server holds the one total; each client:
///   - joins with its current rupees (the room owner's game seeds the wallet) and adopts the total,
///   - sends every local change (pickups +, purchases -) as a delta,
///   - sets its game to each total the server pushes, via the HUD's pending-rupee counter
///     (GameActions) so the on-screen count animates like a normal pickup.
/// Once a pushed total has been applied, the new baseline is that total clamped to this player's
/// wallet size — so the clamp is never mistaken for spending, while anything picked up or spent
/// during the HUD count-up still differs from the baseline and is sent as a local change.
/// </summary>
public class SharedWalletService : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<SharedWalletService>();
    private const int TickMs = 250;
    private static readonly TimeSpan JoinRetry = TimeSpan.FromSeconds(3);

    private readonly IDolphinService _dolphin;
    private readonly SignalRClientService _signalR;
    private readonly RoomSettingsService _room;
    private readonly object _tickLock = new();
    private readonly SceneStabilityGate _scene = new(requiredTicks: 4); // 1s at 4Hz
    private System.Timers.Timer? _timer;

    // Guarded by _tickLock
    private bool _joined;
    private DateTime _nextJoinAttempt;
    private bool _loggedWaiting;
    private int _connectionGeneration;
    private int? _baseline;      // rupees as of our last sync point
    private int? _target;        // latest total pushed by the server, not yet applied
    private bool _awaitingApply; // we queued a change; re-baseline once the game has applied it
    private int _appliedTarget;
    private int _beforeApply;    // the game's rupees when we queued it

    private int _joining;

    /// <summary>The shared wallet total as last reported by the server (null until joined).</summary>
    public int? Total { get; private set; }

    /// <summary>Raised (on a background thread) whenever <see cref="Total"/> changes.</summary>
    public event Action<int?>? TotalChanged;

    private void SetTotal(int? total)
    {
        if (Total == total) return;
        Total = total;
        TotalChanged?.Invoke(total);
    }

    public SharedWalletService(IDolphinService dolphin, SignalRClientService signalR, RoomSettingsService room)
    {
        _dolphin = dolphin;
        _signalR = signalR;
        _room = room;
    }

    /// <summary>The most rupees a wallet of this size holds (dSv_player_status_a_c::getRupeeMax).</summary>
    public static int WalletCapacity(byte walletSize) => walletSize switch
    {
        0 => 200,
        1 => 1000,
        _ => 5000,
    };

    /// <summary>
    /// The sync point after the game finished applying a pushed total: the total clamped to this
    /// wallet — not the game's current value, which may include pickups / purchases made during the
    /// count-up that still have to be sent. If the game still shows exactly what it had before
    /// (the pending-rupee write didn't take effect) <paramref name="writeLost"/> is set and the
    /// current value is used, so we never "send" the unapplied difference back as spending.
    /// </summary>
    public static int SettledBaseline(int appliedTarget, int beforeApply, int current, int capacity, out bool writeLost)
    {
        int expected = Math.Clamp(appliedTarget, 0, capacity);
        writeLost = current == beforeApply && expected != beforeApply;
        return writeLost ? current : expected;
    }

    public void Start()
    {
        if (_timer != null) return;
        _signalR.RupeeTotalReceived += OnRupeeTotalReceived;
        _signalR.Connected += OnConnected;

        _timer = new System.Timers.Timer(TickMs) { AutoReset = true };
        _timer.Elapsed += (_, _) =>
        {
            if (!Monitor.TryEnter(_tickLock)) return;
            try { Tick(); }
            catch (Exception ex) { Logger.Error(ex, "[wallet] tick failed"); }
            finally { Monitor.Exit(_tickLock); }
        };
        _timer.Start();
        Logger.Information("[wallet] shared wallet sync started");
    }

    public void Stop()
    {
        if (_timer == null) return;
        _signalR.RupeeTotalReceived -= OnRupeeTotalReceived;
        _signalR.Connected -= OnConnected;
        _timer.Stop();
        _timer.Dispose();
        _timer = null;
        lock (_tickLock) ResetSync();
        SetTotal(null);
    }

    /// <summary>Leave the wallet; we rejoin (and re-adopt the server's total) on the next tick.</summary>
    private void ResetSync()
    {
        _joined = false;
        _nextJoinAttempt = DateTime.MinValue;
        _loggedWaiting = false;
        _baseline = null;
        _target = null;
        _awaitingApply = false;
    }

    private void OnRupeeTotalReceived(int total)
    {
        lock (_tickLock)
        {
            _target = total;
            if (!_joined) _nextJoinAttempt = DateTime.MinValue; // wallet just got seeded? join now
        }
        SetTotal(total);
    }

    // New connection (the server may have restarted with an empty wallet): rejoin.
    private void OnConnected()
    {
        lock (_tickLock)
        {
            _connectionGeneration++;
            ResetSync();
        }
        SetTotal(null);
    }

    private void Tick()
    {
        // Offline: nothing to sync (Connected rejoins).
        if (!_dolphin.IsConnected || !_signalR.IsConnected) return;

        // Room rule off: keep our own rupees; rejoin (and re-adopt the total) when it's back on.
        if (!_room.Current.SharedWallet)
        {
            if (_joined)
            {
                ResetSync();
                SetTotal(null);
                Logger.Information("[wallet] shared wallet turned off by the room owner");
            }
            return;
        }

        // In gameplay with a settled scene — the title demo has a real Link but an empty default
        // save, and joining from it once seeded the wallet with 0 and wiped the owner's rupees.
        if (!_scene.Check(_dolphin)) return;

        // The game is still applying a queued change — wait for it to land.
        if ((_dolphin.Read(GameMemoryAddresses.Player.PendingRupeeDelta) ?? 0) != 0) return;
        if (_dolphin.Read(GameMemoryAddresses.Player.RupeeCount) is not ushort raw) return;
        int current = raw;
        int capacity = _dolphin.Read(GameMemoryAddresses.Player.CurrentWallet) is byte size
            ? WalletCapacity(size)
            : HubConstants.MaxRupees;

        if (!_joined)
        {
            TryJoin(current);
            return;
        }

        if (_awaitingApply)
        {
            _awaitingApply = false;
            _baseline = SettledBaseline(_appliedTarget, _beforeApply, current, capacity, out bool writeLost);
            if (writeLost)
                Logger.Warning("[wallet] applied total {Target} but the game still shows {Current} — pending-rupee write not taking effect?",
                               _appliedTarget, current);
            // Fall through: a pickup / purchase during the count-up now differs from the baseline.
        }

        // Local change first, so a pickup that races a pushed total isn't overwritten unsent.
        if (_baseline is int b && current != b)
        {
            SendDelta(current - b);
            _baseline = current;
            return; // the server's resulting total arrives as _target shortly
        }

        if (_target is int t)
        {
            _target = null;
            if (Math.Clamp(t, 0, capacity) != current)
            {
                GameActions.SetRupees(_dolphin, t);
                _awaitingApply = true;
                _appliedTarget = t;
                _beforeApply = current;
                Logger.Information("[wallet] shared total {Total} → applying ({Delta:+#;-#})", t, t - current);
            }
        }
    }

    /// <summary>Send a local change; if it wasn't delivered, put it back so the next tick resends it.</summary>
    private void SendDelta(int delta)
    {
        int generation = _connectionGeneration;
        Logger.Information("[wallet] local {Delta:+#;-#} rupees → sending", delta);
        _ = Task.Run(async () =>
        {
            bool sent = false;
            try { sent = await _signalR.SendRupeeDeltaAsync(delta); }
            catch (Exception ex) { Logger.Warning(ex, "[wallet] send failed ({Delta:+#;-#})", delta); }
            if (sent) return;
            lock (_tickLock)
            {
                // Same connection: undo the baseline move so the difference is sent again.
                // (A new connection rejoins and adopts the server's total instead.)
                if (generation == _connectionGeneration && _joined && _baseline is int b)
                    _baseline = b - delta;
            }
        });
    }

    private void TryJoin(int current)
    {
        if (DateTime.UtcNow < _nextJoinAttempt) return;
        if (Interlocked.Exchange(ref _joining, 1) == 1) return;
        _nextJoinAttempt = DateTime.UtcNow + JoinRetry;
        int generation = _connectionGeneration;

        _ = Task.Run(async () =>
        {
            try
            {
                var total = await _signalR.JoinWalletAsync(current);
                if (total == null)
                {
                    bool log;
                    lock (_tickLock)
                    {
                        log = !_loggedWaiting && _signalR.IsConnected && _room.Current.SharedWallet;
                        if (log) _loggedWaiting = true;
                    }
                    if (log)
                        Logger.Information("[wallet] shared wallet not ready — waiting for room owner {Owner} to seed it (retrying)",
                            _room.Current.OwnerName);
                    return;
                }
                lock (_tickLock)
                {
                    if (generation != _connectionGeneration) return; // reconnected meanwhile: join again
                    _baseline = current;
                    _target = total;
                    _joined = true;
                    _loggedWaiting = false;
                }
                SetTotal(total);
                Logger.Information("[wallet] joined shared wallet: {Total} rupees (had {Current})", total, current);
            }
            catch (Exception ex) { Logger.Warning(ex, "[wallet] join failed (retrying)"); }
            finally { Interlocked.Exchange(ref _joining, 0); }
        });
    }

    public void Dispose() => Stop();
}
