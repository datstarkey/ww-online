using Serilog;
using WWOnline.Data;
using WWOnline.Hubs;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>
/// Shared items ("room inventory"). The server's room state is the source of truth:
///   - on attach this game JOINS: the room owner's game seeds an empty room; anyone else's
///     progress merges up into it (bits OR, levels MAX, empty slots filled), then the room is
///     applied to this game exactly (so a joiner missing things gets them);
///   - every tick, items / upgrades this game GAINED since the last sync point (never losses)
///     are sent first, so a pickup is never overwritten by a push that doesn't include it yet;
///   - each room state the server pushes (someone's gain, or the owner's edit, which can
///     remove / downgrade) is written into the game where it differs, then re-baselined.
/// Only synced fields are written (see <see cref="RoomInventoryMemory"/>), and only while the
/// scene is stable (Link present, no stage change pending).
/// </summary>
public class RoomInventorySyncService : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<RoomInventorySyncService>();
    private const int TickMs = 250;
    private const int StableTicksRequired = 2;
    private static readonly TimeSpan JoinRetry = TimeSpan.FromSeconds(3);

    private readonly IDolphinService _dolphin;
    private readonly SignalRClientService _signalR;
    private readonly RoomSettingsService _room;
    private readonly SceneStabilityGate _scene = new(StableTicksRequired);
    private readonly object _tickLock = new();
    private readonly object _roomLock = new();
    private System.Timers.Timer? _timer;

    // Guarded by _tickLock
    private bool _joined;
    private DateTime _nextJoinAttempt;
    private bool _loggedWaiting;
    private RoomInventory? _baseline;    // the game's synced state as of our last sync point
    private RoomInventory? _pending;     // newest room state pushed, not yet applied
    private RoomInventory? _lastApplied; // room state last written to the game
    // Equip bytes Apply wrote (or had to skip), re-checked on the following ticks: the REL's
    // sword / shield swap for a peer's puppet can restore the old byte over ours.
    private byte? _verifySword, _verifyShield;
    private int _verifyTicks;
    private const int EquipVerifyMaxTicks = 8; // 2s

    private int _joining;
    private int _sendsInFlight;

    // Guarded by _roomLock
    private long _roomRevision = -1;

    /// <summary>The room inventory as last seen from the server (null until known).</summary>
    public RoomInventory? Room { get; private set; }

    /// <summary>Raised (on a background thread) when <see cref="Room"/> changes.</summary>
    public event Action<RoomInventory?>? RoomChanged;

    public RoomInventorySyncService(IDolphinService dolphin, SignalRClientService signalR, RoomSettingsService room)
    {
        _dolphin = dolphin;
        _signalR = signalR;
        _room = room;
        // Track the room even while no game is attached, so the owner's Items editor has it.
        _signalR.RoomInventoryReceived += OnRoomInventoryReceived;
        _signalR.ConnectionLost += OnConnectionReset;
        _signalR.Connected += OnConnectionReset;
    }

    /// <summary>True when item edits should go to the room (connected and the SharedItems rule is on).</summary>
    public bool IsRoomMode => _signalR.IsConnected && _room.Current.SharedItems;

    public void Start()
    {
        if (_timer != null) return;
        _timer = new System.Timers.Timer(TickMs) { AutoReset = true };
        _timer.Elapsed += (_, _) =>
        {
            if (!Monitor.TryEnter(_tickLock)) return;
            try { Tick(); }
            catch (Exception ex) { Logger.Error(ex, "[items] tick failed"); }
            finally { Monitor.Exit(_tickLock); }
        };
        _timer.Start();
        Logger.Information("[items] room inventory sync started");
    }

    public void Stop()
    {
        if (_timer == null) return;
        _timer.Stop();
        _timer.Dispose();
        _timer = null;
        lock (_tickLock) ResetSync();
        _scene.Reset();
        Logger.Information("[items] room inventory sync stopped");
    }

    private void ResetSync()
    {
        _joined = false;
        _baseline = null;
        _pending = null;
        _lastApplied = null;
        _verifySword = _verifyShield = null;
        _loggedWaiting = false;
        _nextJoinAttempt = DateTime.MinValue;
    }

    // ── Editor API (Items / Equipment tools) ────────────────────────────────

    /// <summary>A copy of the room to edit: the known room, else fetched, else empty.</summary>
    public async Task<RoomInventory> GetEditableRoomAsync()
    {
        var room = Room;
        if (room == null)
        {
            var fetched = await _signalR.GetRoomInventoryAsync();
            if (fetched != null && fetched.IsValid() && AcceptRoom(fetched.Clone().Normalize()))
                room = Room;
        }
        return room?.Clone() ?? new RoomInventory();
    }

    /// <summary>Room owner: replace the room inventory with <paramref name="requested"/> (the server rejects non-owners).</summary>
    public async Task<bool> SetRoomAsync(RoomInventory requested, string what)
    {
        requested.Normalize();
        if (!requested.IsValid())
        {
            Logger.Warning("[items] room edit '{What}' not sent: invalid room state", what);
            return false;
        }
        var before = Room ?? new RoomInventory();
        var changes = RoomInventory.Describe(before, requested);
        Logger.Information("[items] owner edit '{What}' → requesting: {Changes}", what,
            changes.Count > 0 ? string.Join(", ", changes) : "(no change)");
        if (changes.Count == 0) return true;
        await _signalR.SetRoomInventoryAsync(requested);
        // Optimistic, so a second quick edit builds on this one. The server's push (higher
        // revision) replaces it.
        lock (_roomLock) Room = requested.Clone();
        RoomChanged?.Invoke(Room);
        return true;
    }

    // ── Server events ───────────────────────────────────────────────────────

    private void OnRoomInventoryReceived(RoomInventory room)
    {
        if (room == null || !room.IsValid())
        {
            Logger.Warning("[items] ignored invalid room inventory push");
            return;
        }
        room = room.Clone().Normalize();
        if (!AcceptRoom(room)) return;
        lock (_tickLock)
        {
            _pending = room;
            if (!_joined) _nextJoinAttempt = DateTime.MinValue; // room just got seeded? join now
        }
    }

    /// <summary>Connection dropped, or a new one is up (the server may have restarted, revisions reset): rejoin.</summary>
    private void OnConnectionReset()
    {
        lock (_roomLock)
        {
            _roomRevision = -1;
            Room = null;
        }
        RoomChanged?.Invoke(null);
        lock (_tickLock) ResetSync();
    }

    /// <summary>Record <paramref name="room"/> as the latest unless it's older than what we have.</summary>
    private bool AcceptRoom(RoomInventory room)
    {
        lock (_roomLock)
        {
            if (room.Revision < _roomRevision)
            {
                Logger.Debug("[items] dropped stale room rev {Rev} (have {Have})", room.Revision, _roomRevision);
                return false;
            }
            _roomRevision = room.Revision;
            Room = room;
        }
        RoomChanged?.Invoke(room);
        return true;
    }

    // ── Tick ────────────────────────────────────────────────────────────────

    private void Tick()
    {
        // Offline: sync nothing (gains made meanwhile merge up when we rejoin on Connected).
        if (!_dolphin.IsConnected || !_signalR.IsConnected) return;

        // Room rule off: keep our own items; rejoin (and re-adopt the room) when it's back on.
        if (!_room.Current.SharedItems)
        {
            if (_joined)
            {
                ResetSync();
                Logger.Information("[items] shared items turned off by the room owner — keeping this game's items");
            }
            return;
        }

        if (!_scene.Check(_dolphin)) return; // title screen / loading / stage change
        var local = RoomInventoryMemory.Read(_dolphin);
        if (local == null) return;

        if (!_joined)
        {
            TryJoin(local);
            return;
        }

        // 0. The equipped sword / shield we wrote may have been undone by the REL's equip swap.
        VerifyEquipped(local);

        // 1. Local gains first, so a pickup racing a push isn't overwritten before it's sent.
        if (_baseline != null)
        {
            var gains = local.GainsOver(_baseline);
            if (!gains.IsEmpty)
            {
                var after = _baseline.Clone();
                after.MergeGainsFrom(gains);
                Logger.Information("[items] local gain: {Changes} → sending",
                    string.Join(", ", RoomInventory.Describe(_baseline, after)));
                _baseline = local;
                Interlocked.Increment(ref _sendsInFlight);
                _ = Task.Run(async () =>
                {
                    bool sent = false;
                    try { sent = await _signalR.SendInventoryGainsAsync(gains); }
                    catch (Exception ex) { Logger.Warning(ex, "[items] sending gains failed"); }
                    finally { Interlocked.Decrement(ref _sendsInFlight); }
                    if (!sent)
                    {
                        // Not in the room: rejoin, which merges this game's items up again.
                        Logger.Warning("[items] gains not delivered — will rejoin the room inventory");
                        lock (_tickLock) ResetSync();
                    }
                });
                return; // the resulting room arrives as a push
            }
        }
        _baseline = local; // losses (story events, drinking a bottle...) are never sent

        // 2. Apply the newest room state — but not while our gains are in flight: the push that
        //    includes them is on its way, and an older one would take them away again.
        if (_pending is not { } room || Volatile.Read(ref _sendsInFlight) != 0) return;
        _pending = null;

        // The equipped sword / shield follow the room's CHOICE (a new best sword, or the owner's
        // pick) — not re-forced every push, so a story scene that takes the sword isn't fought.
        bool applySword = _lastApplied == null || room.EquippedSword != _lastApplied.EquippedSword;
        bool applyShield = _lastApplied == null || room.EquippedShield != _lastApplied.EquippedShield;

        var changes = new List<string>();
        _baseline = RoomInventoryMemory.Apply(_dolphin, room, local, applySword, applyShield, changes);
        _lastApplied = room;
        _verifySword = applySword ? room.EquippedSword : null;
        _verifyShield = applyShield ? room.EquippedShield : null;
        _verifyTicks = 0;
        if (changes.Count > 0)
            Logger.Information("[items] applied room rev {Rev}: {Changes}", room.Revision, string.Join(", ", changes));
        else
            Logger.Debug("[items] room rev {Rev} already matches this game", room.Revision);
    }

    /// <summary>
    /// Re-check (with <paramref name="local"/>, read seq-checked) the equip bytes Apply wrote: if one
    /// didn't stick, write it once more; if the REL is mid-swap, try again next tick (for at most
    /// <see cref="EquipVerifyMaxTicks"/>). Only ever after a push that changed the room's choice.
    /// </summary>
    private void VerifyEquipped(RoomInventory local)
    {
        if (_verifySword == null && _verifyShield == null) return;
        _verifySword = Recheck(GameMemoryAddresses.Player.CurrentSword, "sword", _verifySword, local.EquippedSword,
            v => { local.EquippedSword = v; if (_baseline != null) _baseline.EquippedSword = v; });
        _verifyShield = Recheck(GameMemoryAddresses.Player.CurrentShield, "shield", _verifyShield, local.EquippedShield,
            v => { local.EquippedShield = v; if (_baseline != null) _baseline.EquippedShield = v; });
        if (++_verifyTicks >= EquipVerifyMaxTicks) _verifySword = _verifyShield = null;
    }

    /// <summary>Returns the value still to verify (null once it matches or was re-written).</summary>
    private byte? Recheck(MemoryAddress<byte> addr, string what, byte? want, byte have, Action<byte> rewritten)
    {
        if (want is not byte w || have == w) return null;
        if (!EquipSwapGuard.TryWrite(_dolphin, addr, w)) return w; // swap in progress: next tick
        Logger.Information("[items] equipped {What} 0x{Want:X2} didn't stick (game shows 0x{Have:X2}) — re-written", what, w, have);
        rewritten(w);
        return null;
    }

    private void TryJoin(RoomInventory local)
    {
        if (DateTime.UtcNow < _nextJoinAttempt) return;
        if (Interlocked.Exchange(ref _joining, 1) == 1) return;
        _nextJoinAttempt = DateTime.UtcNow + JoinRetry;

        var snapshot = local.Clone();
        _ = Task.Run(async () =>
        {
            try
            {
                var room = await _signalR.JoinRoomInventoryAsync(snapshot);
                if (room == null || !room.IsValid())
                {
                    bool waiting;
                    lock (_tickLock)
                    {
                        waiting = !_loggedWaiting && _signalR.IsConnected && _room.Current.SharedItems;
                        if (waiting) _loggedWaiting = true;
                    }
                    if (waiting)
                        Logger.Information("[items] room inventory not ready — waiting for room owner {Owner} to seed it (retrying)",
                            _room.Current.OwnerName);
                    return;
                }

                room = room.Clone().Normalize();
                bool newest = AcceptRoom(room);
                int differences = RoomInventory.Describe(snapshot, room).Count;
                lock (_tickLock)
                {
                    _baseline = snapshot;
                    if (newest || _pending == null) _pending = room; // else a newer push is already pending
                    _lastApplied = null;
                    _joined = true;
                    _loggedWaiting = false;
                }
                Logger.Information("[items] joined room inventory ({Room}); {N} field(s) differ from this game — applying",
                    room.Summary(), differences);
            }
            catch (Exception ex) { Logger.Warning(ex, "[items] join failed (retrying)"); }
            finally { Interlocked.Exchange(ref _joining, 0); }
        });
    }

    public void Dispose()
    {
        Stop();
        _signalR.RoomInventoryReceived -= OnRoomInventoryReceived;
        _signalR.ConnectionLost -= OnConnectionReset;
        _signalR.Connected -= OnConnectionReset;
    }
}
