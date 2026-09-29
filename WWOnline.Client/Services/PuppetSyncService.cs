using System.Buffers.Binary;
using System.Collections.Concurrent;
using Serilog;
using WWOnline.Data;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>
/// Manages the bridge between network puppet data and Dolphin's emulated GameCube memory.
/// Writes remote player positions/state to fixed memory addresses that the puppet C code reads.
/// Also reads local player state for broadcasting to other players.
/// </summary>
public class PuppetSyncService : IDisposable
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<PuppetSyncService>();

    private readonly IDolphinService _dolphin;
    private readonly Dictionary<string, int> _slotAssignments = new();
    private readonly object _slotLock = new();
    private readonly ConcurrentDictionary<string, PuppetData> _remotePuppets = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastPuppetReceive = new();
    private readonly List<IDisposable> _signalRSubscriptions = new();

    // Diagnostic counters populated by the 20Hz write loop
    private string _lastLocalStage = "";
    private byte _lastLocalRoom;
    private float _lastLocalX;
    private float _lastLocalY;
    private float _lastLocalZ;
    private readonly ConcurrentDictionary<string, bool> _lastSlotVisibility = new();
    private System.Timers.Timer? _syncTimer;
    private CancellationTokenSource? _broadcastCts;
    private int _writeCounter;
    private bool _disposed;

    // Scene-change detection + post-transition hold.
    // When the local player's stage or room changes (or goes unstable), we:
    //   1) invalidate hook state in emulator BSS (zero proc/actor/state arrays, reset spawn_frame)
    //   2) hold all puppet writes for HoldTickCount ticks so the new scene settles
    //      before any active=1 reaches the hook again.
    private const int HoldTickCount = 30;   // 1.5s at 20Hz
    private const int StaleDataMillis = 3000; // drop puppets we haven't heard from in 3s
    private string _prevLocalStage = "";
    private byte _prevLocalRoom;
    private bool _hadStableScene;
    private int _holdTicks;
    private uint _heartbeatCounter;
    private uint _unstableBranchHits;
    private uint _wipeAllCalls;
    private uint _updateRemotePuppetCalls;

    // Periodic one-line "[diag]" summary for the log file (2s at 20Hz) — the log is the
    // primary debugging surface for dev-test runs, so everything the Debug page shows lands here too.
    private const int DiagLogEveryTicks = 40;
    private int _diagTick;

    // Local proc (re)start tracking for PuppetData.Action.ProcSeq (see ReadLocalPlayerState).
    private byte _procSeq;
    private byte _lastProc;
    private float _lastUnderFrame;
    private byte _lastCombo;

    // The local WarpInfo broadcast to peers (entrance + busy), rebuilt every tick.
    private readonly LocalWarpReader _warpReader = new();

    /// <summary>The local <see cref="WarpInfo"/> as last broadcast (null before the first tick).</summary>
    public WarpInfo? LocalWarp { get; private set; }

    // Last local held item / carried kind written to the log ("[held]" line on change).
    private (ushort Item, byte Grab) _lastLoggedHeld = (0, 0);

    // Pre-allocated buffers for the 20Hz write path to avoid GC pressure
    private readonly byte[] _headerBuf = new byte[GameMemoryAddresses.PuppetSync.HeaderSize];
    private readonly byte[][] _slotBufs = new byte[GameMemoryAddresses.PuppetSync.MaxSlots][];
    private readonly byte[] _zeroSlot = new byte[GameMemoryAddresses.PuppetSync.SlotSize];
    private readonly byte[] _boatBuf = new byte[GameMemoryAddresses.PuppetSync.BoatSize];
    private readonly byte[] _boatBodyBuf = new byte[GameMemoryAddresses.PuppetSync.BoatSize - PuppetBoatBodyOffset];
    private readonly byte[] _zeroBoat = new byte[GameMemoryAddresses.PuppetSync.BoatSize];
    private readonly byte[] _boatCannonBuf = new byte[GameMemoryAddresses.PuppetSync.BoatCannonSize];
    private readonly byte[] _boatCraneBuf = new byte[GameMemoryAddresses.PuppetSync.BoatCraneSize];
    private readonly byte[] _zeroBoatCannon = new byte[GameMemoryAddresses.PuppetSync.BoatCannonSize];
    private readonly byte[] _zeroBoatCrane = new byte[GameMemoryAddresses.PuppetSync.BoatCraneSize];
    private readonly byte[] _u32Buf = new byte[4];

    // Guards every emulator write + the shared buffers above: the 20Hz timer, WipeAll and
    // ReleaseSlot run on different threads.
    private readonly object _writeLock = new();

    // A transition hold also waits for the hook to report every slot IDLE (so puppets from the
    // old scene are really deleted before we ask for new ones) — but never longer than this.
    private static readonly TimeSpan HoldIdleTimeout = TimeSpan.FromSeconds(10);
    private DateTime _holdStartedUtc;

    public bool IsActive => _syncTimer?.Enabled ?? false;
    public int ActiveSlotCount { get { lock (_slotLock) return _slotAssignments.Count; } }

    /// <summary>
    /// Live appearance state for the local player. Updated by the UI in real-time.
    /// Broadcast to peers (ReadLocalPlayerState) and published to the puppet REL every tick, which
    /// applies it to the local Link (see <see cref="Services.LocalAppearance"/>).
    /// </summary>
    public AppearanceState LocalAppearance { get; set; } = new();

    // Last LocalAppearance.NeedsRel verdict, for logging transitions only.
    private bool _localLookNeedsRel;

    /// <summary>
    /// "Show player names": whether the REL draws each peer's name above their puppet
    /// (<see cref="PuppetNameTags"/>). Live: published to the game on the next tick.
    /// </summary>
    public bool ShowPlayerNames { get; set; } = true;

    // Player names by connection id, from PlayerJoined, already sanitised for the game font.
    private readonly ConcurrentDictionary<string, string> _playerNames = new();

    // Names block last seen (0 = none yet), for logging changes only.
    private uint _namesBlock;

    // Anim mirror (AnimMirror): each slot's entry for the REL, the sample it came from and its SEQ.
    // SEQ changes only with a new sample: the REL snaps its frames to each new one.
    private readonly byte[]?[] _anmEntries = new byte[]?[GameMemoryAddresses.PuppetSync.MaxSlots];
    private readonly PuppetData?[] _anmSamples = new PuppetData?[GameMemoryAddresses.PuppetSync.MaxSlots];
    private readonly uint[] _anmSeqs = new uint[GameMemoryAddresses.PuppetSync.MaxSlots];
    private uint _anmBlock;

    private readonly GameSettingsService _settingsService;
    private readonly DespawnWorker _despawnWorker;
    private readonly LiveWorldPoke _liveWorld;

    // Last equip ids read outside a REL equip swap (see EquipSwapGuard).
    private byte _lastSword, _lastShield;

    public PuppetSyncService(IDolphinService dolphinService, GameSettingsService settingsService, DespawnWorker despawnWorker,
        LiveWorldPoke liveWorld)
    {
        _dolphin = dolphinService;
        _settingsService = settingsService;
        _despawnWorker = despawnWorker;
        _liveWorld = liveWorld;

        for (int i = 0; i < _slotBufs.Length; i++)
            _slotBufs[i] = new byte[GameMemoryAddresses.PuppetSync.SlotSize];

        // Seed live appearance from saved settings
        var saved = settingsService.Load();
        LocalAppearance = new AppearanceState
        {
            ClothesType = saved.ClothesType,
            ColorR = saved.TunicColorR,
            ColorG = saved.TunicColorG,
            ColorB = saved.TunicColorB,
        };
        ShowPlayerNames = saved.ShowPlayerNames;
    }

    /// <summary>
    /// Start the sync service. Begins writing puppet data to game memory at 20Hz.
    /// </summary>
    public void Start()
    {
        if (_syncTimer != null)
        {
            Logger.Warning("PuppetSyncService already started");
            return;
        }

        Logger.Information("Starting PuppetSyncService (20Hz sync)");

        lock (_writeLock)
            RepairInvalidHookState();

        _syncTimer = new System.Timers.Timer(50); // 20Hz = 50ms
        _syncTimer.Elapsed += (_, _) =>
        {
            // Single writer: skip the tick rather than overlap a slow previous tick or a
            // concurrent WipeAll/ReleaseSlot (they share the header/slot buffers).
            if (!Monitor.TryEnter(_writeLock)) return;
            try
            {
                WriteSlotsToMemory();
                if (Interlocked.Increment(ref _diagTick) % DiagLogEveryTicks == 0)
                    LogDiagnosticsSnapshot();
            }
            finally
            {
                Monitor.Exit(_writeLock);
            }
        };
        _syncTimer.AutoReset = true;
        _syncTimer.Start();
    }

    /// <summary>
    /// Stop the sync service and clear shared memory.
    /// </summary>
    public void Stop()
    {
        Logger.Information("Stopping PuppetSyncService");

        _syncTimer?.Stop();
        _syncTimer?.Dispose();
        _syncTimer = null;

        WipeAll();
        _writeCounter = 0;
    }

    /// <summary>
    /// Clear all puppet state — in-memory dictionaries plus the header and slot bodies in
    /// emulator memory. With the header zeroed the hook sees desiredCount = 0 and deletes every
    /// live puppet itself. The hook's tracking arrays are deliberately left alone (see
    /// <see cref="RepairInvalidHookState"/>).
    /// Safe to call when Dolphin is disconnected: the dictionaries still get cleared.
    /// Call on connection loss, shutdown, or any other "we're no longer authoritative" event.
    /// </summary>
    public void WipeAll()
    {
        _wipeAllCalls++;
        Logger.Information("WipeAll #{N} — despawning all puppets", _wipeAllCalls);

        lock (_slotLock)
            _slotAssignments.Clear();

        _remotePuppets.Clear();
        _lastPuppetReceive.Clear();
        _lastSlotVisibility.Clear();

        if (!_dolphin.IsConnected)
            return;

        lock (_writeLock)
        {
            _hadStableScene = false;
            _holdTicks = 0;
            try
            {
                // magic=0 → hook's desiredCount = 0 → it deletes every puppet it tracks.
                Array.Clear(_headerBuf, 0, _headerBuf.Length);
                _dolphin.WriteMemory(GameMemoryAddresses.PuppetSync.BaseAddress, _headerBuf);

                // Zero every slot body so no stray active=1 survives.
                for (int i = 0; i < GameMemoryAddresses.PuppetSync.MaxSlots; i++)
                    ZeroSlotMemory(i);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during WipeAll");
            }
        }
    }

    /// <summary>Zero a slot body, its boat block and its cannon / crane words (caller holds _writeLock).</summary>
    private void ZeroSlotMemory(int slot)
    {
        _dolphin.WriteMemory(GameMemoryAddresses.PuppetSync.GetSlotBase(slot), _zeroSlot);
        _dolphin.WriteMemory(GameMemoryAddresses.PuppetSync.GetBoatBase(slot), _zeroBoat);
        _dolphin.WriteMemory(GameMemoryAddresses.PuppetSync.GetBoatCannonBase(slot), _zeroBoatCannon);
        _dolphin.WriteMemory(GameMemoryAddresses.PuppetSync.GetBoatCraneBase(slot), _zeroBoatCrane);
    }

    /// <summary>
    /// Write a slot's boat block: the peer's boat while they ride it, zeros otherwise
    /// (PUPPET_BOAT_* in puppet_shared.h). With a boat, its cannon / crane words too
    /// (PUPPET_BOAT_CANNON_* / PUPPET_BOAT_CRANE_*); without one the REL ignores them, so they're left as they are.
    /// </summary>
    private void WriteBoat(int slot, BoatState? boat)
    {
        if (boat == null || !boat.IsValid())
        {
            _dolphin.WriteMemory(GameMemoryAddresses.PuppetSync.GetBoatBase(slot), _zeroBoat);
            return;
        }

        // Parts and body first, FLAGS last: the REL snaps a new boat to the first position it sees
        // with FLAGS set (and a part that has just come out to the angles it sees with FLAGS' PART),
        // so FLAGS must never be visible before them.
        EncodeBoatCannon(boat, _boatCannonBuf);
        _dolphin.WriteMemory(GameMemoryAddresses.PuppetSync.GetBoatCannonBase(slot), _boatCannonBuf);
        EncodeBoatCrane(boat, _boatCraneBuf);
        _dolphin.WriteMemory(GameMemoryAddresses.PuppetSync.GetBoatCraneBase(slot), _boatCraneBuf);

        uint boatBase = GameMemoryAddresses.PuppetSync.GetBoatBase(slot);
        Array.Clear(_boatBuf, 0, _boatBuf.Length);
        WriteBigEndianFloat(_boatBuf, PuppetLayout.PUPPET_BOAT_OFF_POSX, boat.Position.X);
        WriteBigEndianFloat(_boatBuf, PuppetLayout.PUPPET_BOAT_OFF_POSY, boat.Position.Y);
        WriteBigEndianFloat(_boatBuf, PuppetLayout.PUPPET_BOAT_OFF_POSZ, boat.Position.Z);
        WriteBigEndianFloat(_boatBuf, PuppetLayout.PUPPET_BOAT_OFF_SPEED_F, boat.SpeedF);
        WriteBigEndianS16(_boatBuf, PuppetLayout.PUPPET_BOAT_OFF_ROTY, boat.Rotation);
        WriteBigEndianS16(_boatBuf, PuppetLayout.PUPPET_BOAT_OFF_SAIL_ANGLE, boat.SailAngle);
        WriteBigEndianS16(_boatBuf, PuppetLayout.PUPPET_BOAT_OFF_TILLER, boat.Tiller);
        WriteBigEndianS16(_boatBuf, PuppetLayout.PUPPET_BOAT_OFF_HEAD_X, boat.HeadX);
        WriteBigEndianS16(_boatBuf, PuppetLayout.PUPPET_BOAT_OFF_HEAD_Y, boat.HeadY);
        _boatBuf[PuppetLayout.PUPPET_BOAT_OFF_MAST_FRAME] = boat.MastFrame;
        _boatBuf[PuppetLayout.PUPPET_BOAT_OFF_HEAD_FRAME] = boat.HeadFrame;
        Buffer.BlockCopy(_boatBuf, PuppetBoatBodyOffset, _boatBodyBuf, 0, _boatBodyBuf.Length);
        _dolphin.WriteMemory(boatBase + PuppetBoatBodyOffset, _boatBodyBuf);

        WriteBigEndianU32(_u32Buf, 0, BoatFlags(boat));
        _dolphin.WriteMemory(boatBase + PuppetLayout.PUPPET_BOAT_OFF_FLAGS, _u32Buf);
    }

    /// <summary>A riding peer's boat FLAGS word (PUPPET_BOAT_OFF_FLAGS).</summary>
    public static uint BoatFlags(BoatState boat) =>
        PuppetLayout.PUPPET_BOAT_FLAG_ACTIVE
        | (boat.Parked ? PuppetLayout.PUPPET_BOAT_FLAG_PARKED : 0u)
        | (boat.Flying ? PuppetLayout.PUPPET_BOAT_FLAG_FLY : 0u)
        | (boat.MastRaised ? PuppetLayout.PUPPET_BOAT_FLAG_MAST_ON : 0u)
        | (boat.MastHidden ? PuppetLayout.PUPPET_BOAT_FLAG_MAST_HIDE : 0u)
        | (uint)(boat.Part & PuppetLayout.PUPPET_BOAT_PART_MASK) << PuppetLayout.PUPPET_BOAT_PART_SHIFT
        | (uint)(boat.HeadBck & PuppetLayout.PUPPET_BOAT_HEAD_BCK_MASK) << PuppetLayout.PUPPET_BOAT_HEAD_BCK_SHIFT;

    /// <summary>
    /// A player's hull colour for the slot (PUPPET_SLOT_OFF_BOAT_COLOR): RGB565, 0 = the classic red. A real colour
    /// that encodes to 0 (black) is sent as 0x0001 so it isn't read as "classic".
    /// </summary>
    public static ushort BoatColor565(AppearanceState a)
    {
        if (a.BoatR == 0 && a.BoatG == 0 && a.BoatB == 0) return 0;
        ushort c = (ushort)((a.BoatR >> 3) << 11 | (a.BoatG >> 2) << 5 | a.BoatB >> 3);
        return c == 0 ? (ushort)1 : c;
    }

    /// <summary>A boat's cannon word (PUPPET_BOAT_CANNON_*, big-endian) into <paramref name="dest"/>.</summary>
    public static void EncodeBoatCannon(BoatState boat, byte[] dest)
    {
        Array.Clear(dest, 0, PuppetLayout.PUPPET_BOAT_CANNON_SIZE);
        WriteBigEndianS16(dest, PuppetLayout.PUPPET_BOAT_CANNON_OFF_YAW, boat.CannonYaw);
        WriteBigEndianS16(dest, PuppetLayout.PUPPET_BOAT_CANNON_OFF_PITCH, boat.CannonPitch);
    }

    /// <summary>A boat's crane word (PUPPET_BOAT_CRANE_*, big-endian) into <paramref name="dest"/>.</summary>
    public static void EncodeBoatCrane(BoatState boat, byte[] dest)
    {
        Array.Clear(dest, 0, PuppetLayout.PUPPET_BOAT_CRANE_SIZE);
        WriteBigEndianS16(dest, PuppetLayout.PUPPET_BOAT_CRANE_OFF_ANGLE, boat.CraneAngle);
        dest[PuppetLayout.PUPPET_BOAT_CRANE_OFF_ROPE] = boat.RopeLength;
    }

    /// <summary>Everything after the FLAGS word (PUPPET_BOAT_OFF_FLAGS is the first field).</summary>
    private const int PuppetBoatBodyOffset = PuppetLayout.PUPPET_BOAT_OFF_FLAGS + 4;

    /// <summary>
    /// Restart the hook's 60-frame settle delay so it waits before (re)spawning in a new scene.
    /// </summary>
    private void ResetSettleDelay()
    {
        Array.Clear(_u32Buf, 0, 4);
        _dolphin.WriteMemory(GameMemoryAddresses.PuppetSync.SpawnFrameCounterAddr, _u32Buf);
    }

    /// <summary>
    /// The hook owns the proc-ID / actor / spawn-state arrays. Zeroing a live entry orphans the
    /// puppet: it lives in the stage layer (room changes don't delete it), keeps running, and the
    /// hook spawns a duplicate with another 0xB0000 heap. So C# only repairs entries holding a
    /// value the hook can't have written (uninitialised memory after boot) — never live ones.
    /// To despawn, write desiredCount 0 and let the hook delete (stale IDs are harmless: process
    /// IDs only increase and deleting a missing one is a no-op).
    /// </summary>
    private void RepairInvalidHookState()
    {
        if (!_dolphin.IsConnected) return;

        var states = _dolphin.ReadMemory(GameMemoryAddresses.PuppetSync.SpawnStateAddr, 4 * GameMemoryAddresses.PuppetSync.MaxSlots);
        if (states == null) return;

        for (int i = 0; i < GameMemoryAddresses.PuppetSync.MaxSlots; i++)
        {
            uint state = ReadBigEndianU32(states, i * 4);
            if (state <= PuppetLayout.SPAWN_FOUND) continue;

            Logger.Warning("[puppet] hook slot {Slot} had invalid spawn state 0x{State:X} — resetting entry", i, state);
            Array.Clear(_u32Buf, 0, 4);
            _dolphin.WriteMemory(GameMemoryAddresses.PuppetSync.ProcIdsAddr + (uint)(i * 4), _u32Buf);
            _dolphin.WriteMemory(GameMemoryAddresses.PuppetSync.ActorPtrsAddr + (uint)(i * 4), _u32Buf);
            _dolphin.WriteMemory(GameMemoryAddresses.PuppetSync.SpawnStateAddr + (uint)(i * 4), _u32Buf);
        }
    }

    /// <summary>True once the hook has deleted/reset every slot (all spawn states IDLE).</summary>
    private bool AllHookSlotsIdle()
    {
        var states = _dolphin.ReadMemory(GameMemoryAddresses.PuppetSync.SpawnStateAddr, 4 * GameMemoryAddresses.PuppetSync.MaxSlots);
        if (states == null) return false;
        for (int i = 0; i < GameMemoryAddresses.PuppetSync.MaxSlots; i++)
        {
            if (ReadBigEndianU32(states, i * 4) != PuppetLayout.SPAWN_IDLE) return false;
        }
        return true;
    }

    /// <summary>
    /// Write the sync header. desiredCount is what the hook reads as "keep slots 0..N-1 spawned",
    /// so callers pass highest active slot + 1 (not the number of visible slots — a gap at slot 0
    /// would otherwise hide slot 1 forever). Inactive slots below N are hidden by puppet_draw.
    /// </summary>
    private void WriteSyncHeader(uint magic, int desiredCount)
    {
        if (!_dolphin.IsConnected) return;
        Array.Clear(_headerBuf, 0, _headerBuf.Length);
        WriteBigEndianU32(_headerBuf, PuppetLayout.PUPPET_HDR_OFF_MAGIC, magic);
        WriteBigEndianU32(_headerBuf, PuppetLayout.PUPPET_HDR_OFF_NUM_ACTIVE, (uint)desiredCount);
        WriteBigEndianU32(_headerBuf, PuppetLayout.PUPPET_HDR_OFF_WRITE_COUNTER, (uint)Interlocked.Increment(ref _writeCounter));
        _dolphin.WriteMemory(GameMemoryAddresses.PuppetSync.BaseAddress, _headerBuf);

        // DIAGNOSTIC: mirror the desired count we just wrote + a write counter to a
        // scratch BSS address so the UI can compare "what C# thought it wrote" vs
        // "what the hook read as desiredCount". If these disagree, something between
        // the write and the read is stomping the header.
        WriteBigEndianU32(_u32Buf, 0, (uint)desiredCount);
        _dolphin.WriteMemory(GameMemoryAddresses.PuppetSync.ClientDbgLastActiveAddr, _u32Buf);
        WriteBigEndianU32(_u32Buf, 0, (uint)_writeCounter);
        _dolphin.WriteMemory(GameMemoryAddresses.PuppetSync.ClientDbgWriteCounterAddr, _u32Buf);
    }

    /// <summary>
    /// Track a SignalR subscription for cleanup on StopBroadcast.
    /// </summary>
    public void TrackSubscription(IDisposable subscription)
    {
        lock (_slotLock)
            _signalRSubscriptions.Add(subscription);
    }

    /// <summary>
    /// Start the broadcast loop cancellation token. Cancels any prior broadcast.
    /// </summary>
    public CancellationToken StartBroadcast()
    {
        lock (_slotLock)
        {
            _broadcastCts?.Cancel();
            _broadcastCts?.Dispose();
            _broadcastCts = new CancellationTokenSource();
            return _broadcastCts.Token;
        }
    }

    /// <summary>
    /// Cancel the broadcast loop, dispose tracked SignalR subscriptions, and wipe puppet state
    /// so stale data doesn't linger in emulator memory while we're offline.
    /// </summary>
    public void StopBroadcast()
    {
        List<IDisposable> subs;
        lock (_slotLock)
        {
            _broadcastCts?.Cancel();
            _broadcastCts?.Dispose();
            _broadcastCts = null;
            subs = new List<IDisposable>(_signalRSubscriptions);
            _signalRSubscriptions.Clear();
        }
        foreach (var sub in subs)
            sub.Dispose();

        WipeAll();
    }

    /// <summary>
    /// Hard reset after a connection loss (dropped, reconnecting or closed). Wipes all remote state so stale
    /// puppets from before the disconnect don't stay spawned in-game.
    /// </summary>
    public void OnConnectionLost() => WipeAll();

    /// <summary>
    /// Assign a slot for a remote player. Returns slot index (0-2) or -1 if full.
    /// </summary>
    public int AssignSlot(string playerId)
    {
        lock (_slotLock)
        {
            if (_slotAssignments.TryGetValue(playerId, out int existing))
            {
                Logger.Debug("Player {PlayerId} already assigned to slot {Slot}", playerId, existing);
                return existing;
            }

            // Find first free slot
            for (int i = 0; i < GameMemoryAddresses.PuppetSync.MaxSlots; i++)
            {
                if (!_slotAssignments.ContainsValue(i))
                {
                    _slotAssignments[playerId] = i;
                    Logger.Information("Assigned player {PlayerId} to puppet slot {Slot}", playerId, i);
                    return i;
                }
            }
        }

        Logger.Warning("No free puppet slots for player {PlayerId}", playerId);
        return -1;
    }

    /// <summary>
    /// Release a slot when a player disconnects. Zeroes the slot body immediately (active=0 hides
    /// the puppet in puppet_draw); the next 20Hz tick recomputes desiredCount so the hook deletes it.
    /// </summary>
    public void ReleaseSlot(string playerId)
    {
        int slot;
        lock (_slotLock)
        {
            if (!_slotAssignments.TryGetValue(playerId, out slot))
                return;
            _slotAssignments.Remove(playerId);
        }

        _remotePuppets.TryRemove(playerId, out _);
        _lastPuppetReceive.TryRemove(playerId, out _);
        _lastSlotVisibility.TryRemove(playerId, out _);
        _playerNames.TryRemove(playerId, out _);

        if (_dolphin.IsConnected)
        {
            lock (_writeLock)
                ZeroSlotMemory(slot);
        }

        Logger.Information("Released puppet slot {Slot} for player {PlayerId}", slot, playerId);
    }

    /// <summary>
    /// Remember a remote player's name (from PlayerJoined) for the name above their puppet.
    /// </summary>
    public void SetPlayerName(string playerId, string? playerName)
    {
        if (string.IsNullOrEmpty(playerId))
            return;
        string sanitized = PuppetNameTags.Sanitize(playerName);
        if (sanitized.Length == 0)
            _playerNames.TryRemove(playerId, out _);
        else
            _playerNames[playerId] = sanitized;
    }

    /// <summary>
    /// The name drawn above each slot's puppet ("" for an empty slot): the player's join name,
    /// else the name in their puppet data, sanitised for the game font (<see cref="PuppetNameTags"/>).
    /// </summary>
    public IReadOnlyList<string> GetSlotNames()
    {
        var names = new string[GameMemoryAddresses.PuppetSync.MaxSlots];
        for (int i = 0; i < names.Length; i++)
        {
            string? playerId = GetPlayerInSlot(i);
            if (playerId == null)
            {
                names[i] = "";
                continue;
            }
            string? name = _playerNames.TryGetValue(playerId, out var joined) ? joined
                : _remotePuppets.TryGetValue(playerId, out var puppet) ? puppet.PlayerName : null;
            names[i] = PuppetNameTags.DisplayName(name, i);
        }
        return names;
    }

    /// <summary>
    /// Keep the REL's names block in line with the slots and the "Show player names" choice. Writes
    /// only what changed; a no-op until the REL has created the block (first puppet since boot).
    /// </summary>
    private void PublishPlayerNames()
    {
        var names = GetSlotNames();
        var result = PuppetNameTags.Publish(_dolphin, ShowPlayerNames, names);
        uint block = result?.Block ?? 0;
        if (block != _namesBlock)
        {
            Logger.Information(block != 0 ? "[names] names block at 0x{Block:X8}" : "[names] names block gone (was 0x{Old:X8})",
                               block != 0 ? block : _namesBlock);
            _namesBlock = block;
        }
        if (result is not { } r)
            return;
        if (r.FlagsWritten)
            Logger.Information("[names] show player names: {Show}", ShowPlayerNames);
        for (int i = 0; i < names.Count; i++)
        {
            if ((r.SlotsWritten & (1 << i)) != 0)
                Logger.Information("[names] slot {Slot} name '{Name}'", i, names[i]);
        }
    }

    /// <summary>
    /// Update remote puppet data received from the network.
    /// </summary>
    public void UpdateRemotePuppet(string playerId, PuppetData data)
    {
        _updateRemotePuppetCalls++;

        // Auto-assign slot if not yet assigned
        bool assigned;
        lock (_slotLock)
            assigned = _slotAssignments.ContainsKey(playerId);

        if (!assigned)
        {
            int slot = AssignSlot(playerId);
            if (slot < 0)
                return; // No free slots
        }

        _remotePuppets[playerId] = data;
        _lastPuppetReceive[playerId] = DateTime.UtcNow;
    }

    /// <summary>A peer's last puppet data and how long ago it arrived (WarpService).</summary>
    public bool TryGetRemotePuppet(string playerId, out PuppetData data, out TimeSpan age)
    {
        age = TimeSpan.MaxValue;
        if (!_remotePuppets.TryGetValue(playerId, out data!))
            return false;
        if (_lastPuppetReceive.TryGetValue(playerId, out var at))
            age = DateTime.UtcNow - at;
        return true;
    }

    /// <summary>
    /// Read the debug counters written by the in-game puppet hook code.
    /// Returns null if Dolphin isn't connected.
    /// </summary>
    public PuppetSpawnCounters? ReadSpawnCounters()
    {
        if (!_dolphin.IsConnected) return null;
        return new PuppetSpawnCounters
        {
            MagicDetected    = ReadU32(GameMemoryAddresses.PuppetSync.DbgMagicDetectedAddr),
            CreateAttempts   = ReadU32(GameMemoryAddresses.PuppetSync.DbgCreateAttemptsAddr),
            CreateSuccesses  = ReadU32(GameMemoryAddresses.PuppetSync.DbgCreateSuccessesAddr),
            CreateFailures   = ReadU32(GameMemoryAddresses.PuppetSync.DbgCreateFailuresAddr),
            LastDesired      = ReadU32(GameMemoryAddresses.PuppetSync.DbgLastDesiredAddr),
            LastPid          = ReadU32(GameMemoryAddresses.PuppetSync.DbgLastPidAddr),
            DeleteIssued           = ReadU32(GameMemoryAddresses.PuppetSync.DbgDeleteIssuedAddr),
            ClientLastWroteActive  = ReadU32(GameMemoryAddresses.PuppetSync.ClientDbgLastActiveAddr),
            ClientWriteCounter     = ReadU32(GameMemoryAddresses.PuppetSync.ClientDbgWriteCounterAddr),
            UnstableBranchHits     = ReadU32(GameMemoryAddresses.PuppetSync.ClientDbgUnstableHitsAddr),
            StabilityBits          = ReadU32(GameMemoryAddresses.PuppetSync.ClientDbgStabilityBitsAddr),
            Slot0VisibilityGates   = ReadU32(GameMemoryAddresses.PuppetSync.ClientDbgSlot0GatesAddr),
            UpdateRemotePuppetCalls = _updateRemotePuppetCalls,
            WipeAllCalls           = _wipeAllCalls,
        };
    }

    /// <summary>
    /// One grep-able line with everything needed to tell where the puppet pipeline stops:
    /// network rx → slot assignment → visibility gates → header write → hook spawn counters.
    /// </summary>
    private void LogDiagnosticsSnapshot()
    {
        try
        {
            if (!_dolphin.IsConnected) return;

            var slots = GetDiagnostics()
                .OrderBy(r => r.Slot)
                .Select(r => $"{r.Slot}:{r.ShortId} vis={(r.IsVisible ? 1 : 0)} data={(r.HasPuppetData ? 1 : 0)} " +
                             $"age={r.AgeText} at={r.PuppetStage}:{r.PuppetRoom} pos={r.PositionText} dist={r.DistanceText}");
            var c = ReadSpawnCounters();
            if (c == null) return;

            Logger.Information(
                "[diag] local={Stage}:{Room} pos=({X:F0},{Y:F0},{Z:F0}) hold={Hold} slots=[{Slots}] " +
                "hook: magic={Magic} desired={Desired} create={Att}/{Ok}/{Fail} lastPid=0x{Pid:X} deletes={Del} " +
                "client: wroteActive={Wrote} gates=0x{Gates:X2} unstable={Unstable} stab=0x{Stab:X} rx={Rx} wipes={Wipes}",
                _lastLocalStage, _lastLocalRoom, _lastLocalX, _lastLocalY, _lastLocalZ, _holdTicks,
                string.Join(" | ", slots),
                c.MagicDetected, c.LastDesired, c.CreateAttempts, c.CreateSuccesses, c.CreateFailures, c.LastPid, c.DeleteIssued,
                c.ClientLastWroteActive, c.Slot0VisibilityGates, c.UnstableBranchHits, c.StabilityBits,
                c.UpdateRemotePuppetCalls, c.WipeAllCalls);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "[diag] snapshot failed");
        }
    }

    private static string ShortId(string playerId) => playerId.Length > 8 ? playerId[..8] : playerId;

    private uint ReadU32(uint address)
    {
        var bytes = _dolphin.ReadMemory(address, 4);
        return bytes == null ? 0u : ReadBigEndianU32(bytes, 0);
    }

    /// <summary>Read a big-endian pointer; 0 unless it points into game RAM. use_extra_memory.asm
    /// moves MEM1's end to 0x83000000, so heaps (and actors) can sit above the stock 24MB; same
    /// bound as LocalAppearance / WorldFlagSyncService.</summary>
    private uint ReadPointer(uint address)
    {
        uint ptr = ReadU32(address);
        return ptr is >= 0x80000000 and < 0x84000000 ? ptr : 0;
    }

    /// <summary>
    /// Snapshot of the current puppet sync state for UI diagnostics.
    /// </summary>
    public IReadOnlyList<PuppetDiagnosticRow> GetDiagnostics()
    {
        Dictionary<string, int> slots;
        lock (_slotLock) slots = new Dictionary<string, int>(_slotAssignments);

        var result = new List<PuppetDiagnosticRow>(slots.Count);
        foreach (var (playerId, slot) in slots)
        {
            _remotePuppets.TryGetValue(playerId, out var puppet);
            var hasLastRx = _lastPuppetReceive.TryGetValue(playerId, out var lastRx);
            _lastSlotVisibility.TryGetValue(playerId, out var wasVisible);
            result.Add(new PuppetDiagnosticRow
            {
                PlayerId = playerId,
                Slot = slot,
                PuppetStage = puppet?.StageName ?? "",
                PuppetRoom = puppet?.RoomNumber ?? 0,
                LocalStage = _lastLocalStage,
                LocalRoom = _lastLocalRoom,
                IsVisible = wasVisible,
                LastPuppetReceived = hasLastRx ? lastRx : null,
                HasPuppetData = puppet != null,
                PuppetX = puppet?.Position.X ?? 0,
                PuppetY = puppet?.Position.Y ?? 0,
                PuppetZ = puppet?.Position.Z ?? 0,
                LocalX = _lastLocalX,
                LocalY = _lastLocalY,
                LocalZ = _lastLocalZ,
            });
        }
        return result;
    }

    /// <summary>
    /// Write all puppet slot data to Dolphin's emulated memory.
    /// Called at 20Hz by the sync timer.
    ///
    /// Flow:
    ///   1) Read local stage/room/linkPtr to determine if the scene is stable.
    ///   2) On transition (stage or room changed, except between sea squares, or
    ///      unstable→stable; see PuppetVisibility): invalidate hook state
    ///      in emulator BSS and start a hold timer so no active=1 reaches the hook until the
    ///      new scene has settled.
    ///   3) While holding (or when unstable): write a zero header + zero slots so the hook's
    ///      despawn path fires cleanly.
    ///   4) Otherwise: write per-slot data with same-stage/same-room (at sea: in sight) filtering and
    ///      a staleness guard (drop puppets we haven't heard from in StaleDataMillis).
    /// </summary>
    private void WriteSlotsToMemory()
    {
        if (!_dolphin.IsConnected)
            return;

        try
        {
            // Always advance the client-alive heartbeat so the hook can detect a dead client.
            _heartbeatCounter++;
            WriteBigEndianU32(_u32Buf, 0, _heartbeatCounter);
            _dolphin.WriteMemory(GameMemoryAddresses.PuppetSync.ClientHeartbeatAddr, _u32Buf);

            // The local player's outfit/colour for the puppet REL (it applies it to the local Link).
            var localLook = LocalAppearance;
            Services.LocalAppearance.Publish(_dolphin, localLook);

            // The peers' names (and whether to show them) for the REL's name tags.
            PublishPlayerNames();

            // Read local player's current stage/room for visibility filtering + transition detection.
            string localStage = "";
            byte localRoom = 0;
            var stageBytes = _dolphin.ReadMemory(GameMemoryAddresses.Stage.CurrentStageName.Address, 8);
            if (stageBytes != null)
            {
                int nullIdx = Array.IndexOf(stageBytes, (byte)0);
                int len = nullIdx >= 0 ? nullIdx : stageBytes.Length;
                localStage = System.Text.Encoding.ASCII.GetString(stageBytes, 0, len);
            }
            var roomVal = _dolphin.ReadMemory(GameMemoryAddresses.Stage.CurrentRoomNumber.Address, 1);
            if (roomVal != null && roomVal.Length > 0)
                localRoom = roomVal[0];

            var linkPtr = _dolphin.Read(GameMemoryAddresses.Player.LinkActorPointer);
            bool linkAlive = linkPtr != null && linkPtr.Value != 0;

            _lastLocalStage = localStage;
            _lastLocalRoom = localRoom;

            if (linkAlive)
            {
                uint actorBase = linkPtr!.Value;
                _lastLocalX = _dolphin.ReadWithOffset(actorBase, GameMemoryAddresses.Player.LinkPositionXOffset) ?? 0;
                _lastLocalY = _dolphin.ReadWithOffset(actorBase, GameMemoryAddresses.Player.LinkPositionYOffset) ?? 0;
                _lastLocalZ = _dolphin.ReadWithOffset(actorBase, GameMemoryAddresses.Player.LinkPositionZOffset) ?? 0;
            }

            // A pending scene change (play.mNextStage.mEnable) counts as unstable: a sea-to-sea
            // reload (Ballad of Gales, cyclone, game-over restart) keeps the stage name and a room
            // change there isn't a scene change (PuppetVisibility), so this is what catches it.
            var nextStage = _dolphin.ReadMemory(GameMemoryAddresses.WorldFlags.NextStageEnable, 1);
            bool stageChangePending = nextStage != null && nextStage.Length > 0 && nextStage[0] != 0;

            bool stable = linkAlive && !string.IsNullOrEmpty(localStage) && !stageChangePending;
            bool sceneChanged = stable && _hadStableScene &&
                                PuppetVisibility.IsSceneChange(_prevLocalStage, _prevLocalRoom, localStage, localRoom);

            // Moving between sea squares keeps the puppets (PuppetVisibility), but the new
            // square's placed items still need a fresh despawn scan.
            if (stable && _hadStableScene && !sceneChanged && localRoom != _prevLocalRoom)
            {
                Logger.Information("[puppet] sea square {OldRoom} -> {NewRoom} — keeping puppets", _prevLocalRoom, localRoom);
                _despawnWorker.MarkChanged(_dolphin);
            }

            if (!stable || sceneChanged)
            {
                // Bump a diagnostic counter + mirror stability inputs so the UI can see exactly
                // why we keep re-entering the hold window every tick.
                _unstableBranchHits++;
                WriteBigEndianU32(_u32Buf, 0, _unstableBranchHits);
                _dolphin.WriteMemory(GameMemoryAddresses.PuppetSync.ClientDbgUnstableHitsAddr, _u32Buf);
                WriteBigEndianU32(_u32Buf, 0, (uint)((stable ? 1 : 0) | (sceneChanged ? 2 : 0) | (linkAlive ? 4 : 0)));
                _dolphin.WriteMemory(GameMemoryAddresses.PuppetSync.ClientDbgStabilityBitsAddr, _u32Buf);

                // Enter or extend the hold window: desiredCount 0 makes the hook delete every
                // puppet it tracks, and the settle delay stops it respawning until the new
                // scene has settled.
                if (sceneChanged)
                {
                    Logger.Information("[puppet] scene changed {OldStage}:{OldRoom} -> {NewStage}:{NewRoom} — despawning until settled",
                                       _prevLocalStage, _prevLocalRoom, localStage, localRoom);
                    _despawnWorker.MarkChanged(_dolphin); // new placed items: the REL's last scan no longer counts
                }

                ResetSettleDelay();
                for (int i = 0; i < GameMemoryAddresses.PuppetSync.MaxSlots; i++)
                    ZeroSlotMemory(i);
                WriteSyncHeader(GameMemoryAddresses.PuppetSync.Magic, 0);

                _holdStartedUtc = DateTime.UtcNow; // idle-wait timeout counts from the last unstable tick
                _holdTicks = HoldTickCount;
                _prevLocalStage = localStage;
                _prevLocalRoom = localRoom;
                _hadStableScene = stable;
                return;
            }

            _prevLocalStage = localStage;
            _prevLocalRoom = localRoom;
            _hadStableScene = true;

            if (_holdTicks > 0)
            {
                // Keep slots zeroed while the new scene settles AND until the hook has deleted
                // the old scene's puppets (all slots IDLE) — asking for new puppets before that
                // would leave the old actors orphaned.
                bool idle = AllHookSlotsIdle();
                bool timedOut = DateTime.UtcNow - _holdStartedUtc > HoldIdleTimeout;
                if (_holdTicks > 1 || (!idle && !timedOut))
                {
                    if (_holdTicks > 1) _holdTicks--;
                    for (int i = 0; i < GameMemoryAddresses.PuppetSync.MaxSlots; i++)
                        ZeroSlotMemory(i);
                    WriteSyncHeader(GameMemoryAddresses.PuppetSync.Magic, 0);
                    return;
                }
                if (!idle)
                    Logger.Warning("[puppet] hook slots still not IDLE {Seconds:F0}s after scene change — resuming anyway",
                                   HoldIdleTimeout.TotalSeconds);
                _holdTicks = 0;
            }

            int highestActiveSlot = -1;
            DateTime now = DateTime.UtcNow;

            // Per-slot check diagnostics — written to scratch BSS so the UI can show
            // WHERE in the visibility chain we're dropping. Each bit is a gate that
            // passed for slot 0 on the most recent tick.
            //   bit 0: playerId != null
            //   bit 1: _remotePuppets had an entry for that id
            //   bit 2: fresh (within StaleDataMillis)
            //   bit 3: puppet.StageName is non-empty
            //   bit 4: puppet.StageName matches localStage
            //   bit 5: puppet.RoomNumber matches localRoom
            uint slot0Diag = 0;

            for (int i = 0; i < GameMemoryAddresses.PuppetSync.MaxSlots; i++)
            {
                uint slotBase = GameMemoryAddresses.PuppetSync.GetSlotBase(i);
                var slotData = _slotBufs[i];
                Array.Clear(slotData, 0, slotData.Length);
                BoatState? slotBoat = null;
                _anmEntries[i] = null;

                string? playerId = GetPlayerInSlot(i);
                if (i == 0 && playerId != null) slot0Diag |= 0x01;

                if (playerId != null && _remotePuppets.TryGetValue(playerId, out var puppet))
                {
                    if (i == 0) slot0Diag |= 0x02;

                    // Drop puppets we haven't heard from recently — covers "peer crashed, server
                    // didn't send PlayerLeft" and similar zombie cases.
                    bool fresh = !_lastPuppetReceive.TryGetValue(playerId, out var lastRx)
                                 || (now - lastRx).TotalMilliseconds < StaleDataMillis;
                    if (i == 0 && fresh) slot0Diag |= 0x04;

                    // Only show puppet if remote player is in the same stage and room (at sea:
                    // within sight distance — see PuppetVisibility).
                    bool stageNonEmpty = !string.IsNullOrEmpty(puppet.StageName);
                    bool stageMatches = stageNonEmpty && puppet.StageName == localStage;
                    bool roomMatches = puppet.RoomNumber == localRoom;
                    if (i == 0 && stageNonEmpty) slot0Diag |= 0x08;
                    if (i == 0 && stageMatches) slot0Diag |= 0x10;
                    if (i == 0 && roomMatches) slot0Diag |= 0x20;

                    bool hadVisibility = _lastSlotVisibility.TryGetValue(playerId, out var wasVisible);
                    bool sameLocation = fresh &&
                        PuppetVisibility.IsSameLocation(localStage, localRoom, _lastLocalX, _lastLocalZ, puppet,
                                                        hadVisibility && wasVisible);

                    if (!hadVisibility || wasVisible != sameLocation)
                        Logger.Information(
                            "[puppet] slot {Slot} ({Player}) visible {Old} -> {New} (fresh={Fresh} theirs={TheirStage}:{TheirRoom} ours={OurStage}:{OurRoom})",
                            i, ShortId(playerId), hadVisibility ? wasVisible : (bool?)null, sameLocation,
                            fresh, puppet.StageName, puppet.RoomNumber, localStage, localRoom);
                    _lastSlotVisibility[playerId] = sameLocation;

                    if (sameLocation)
                    {
                        highestActiveSlot = i;

                        // Active slot
                        WriteBigEndianU32(slotData, GameMemoryAddresses.PuppetSync.SlotOffset_Active, 1);
                        WriteBigEndianFloat(slotData, GameMemoryAddresses.PuppetSync.SlotOffset_PosX, puppet.Position.X);
                        WriteBigEndianFloat(slotData, GameMemoryAddresses.PuppetSync.SlotOffset_PosY, puppet.Position.Y);
                        WriteBigEndianFloat(slotData, GameMemoryAddresses.PuppetSync.SlotOffset_PosZ, puppet.Position.Z);
                        WriteBigEndianS16(slotData, GameMemoryAddresses.PuppetSync.SlotOffset_RotY, (short)puppet.Rotation);
                        WriteBigEndianU16(slotData, GameMemoryAddresses.PuppetSync.SlotOffset_AnimId, puppet.Animation.UpperBodyAnimation);
                        WriteBigEndianFloat(slotData, GameMemoryAddresses.PuppetSync.SlotOffset_AnimSpeed, puppet.Animation.AnimationSpeed);
                        WriteBigEndianU32(slotData, GameMemoryAddresses.PuppetSync.SlotOffset_StateFlags, puppet.Action.StateFlags);
                        // Prefer raw mCurProc from the broadcaster; fall back to StateFlags-derived MOVE/WAIT
                        // so older clients without CurProc still drive the puppet.
                        slotData[GameMemoryAddresses.PuppetSync.SlotOffset_CurProc] = puppet.Action.CurProc != 0
                            ? puppet.Action.CurProc
                            : (puppet.Action.StateFlags != 0 ? (byte)0x06 : (byte)0x04);
                        slotData[GameMemoryAddresses.PuppetSync.SlotOffset_EquipSword] = puppet.Equipment.SwordId;
                        slotData[GameMemoryAddresses.PuppetSync.SlotOffset_EquipShield] = puppet.Equipment.ShieldId;

                        // Use actual velocity from remote player, fallback to walk speed if moving but no velocity data
                        float velocity = puppet.Action.Velocity > 0.5f
                            ? puppet.Action.Velocity
                            : (puppet.Action.StateFlags != 0 ? 12.0f : 0.0f);
                        WriteBigEndianFloat(slotData, GameMemoryAddresses.PuppetSync.SlotOffset_VelocityF, velocity);
                        WriteBigEndianS16(slotData, GameMemoryAddresses.PuppetSync.SlotOffset_StickAngle, puppet.Action.StickAngle);
                        WriteBigEndianU16(slotData, GameMemoryAddresses.PuppetSync.SlotOffset_EquipItem, puppet.Equipment.ItemInHand);
                        slotData[GameMemoryAddresses.PuppetSync.SlotOffset_ActionFlags] = puppet.Action.ActionFlags;
                        slotData[GameMemoryAddresses.PuppetSync.SlotOffset_ProcSeq] = puppet.Action.ProcSeq;
                        WriteBigEndianU16(slotData, GameMemoryAddresses.PuppetSync.SlotOffset_SeqNum, 0);

                        // Appearance data
                        slotData[GameMemoryAddresses.PuppetSync.SlotOffset_ClothesType] = puppet.Appearance.ClothesType;
                        slotData[GameMemoryAddresses.PuppetSync.SlotOffset_ColorR] = puppet.Appearance.ColorR;
                        slotData[GameMemoryAddresses.PuppetSync.SlotOffset_ColorG] = puppet.Appearance.ColorG;
                        slotData[GameMemoryAddresses.PuppetSync.SlotOffset_ColorB] = puppet.Appearance.ColorB;
                        WriteBigEndianU16(slotData, PuppetLayout.PUPPET_SLOT_OFF_BOAT_COLOR, BoatColor565(puppet.Appearance));

                        // v2 extended state — full animation-relevant Link fields.
                        WriteBigEndianFloat(slotData, GameMemoryAddresses.PuppetSync.SlotOffset_SpeedF,         puppet.Action.SpeedF);
                        WriteBigEndianFloat(slotData, GameMemoryAddresses.PuppetSync.SlotOffset_MaxNormalSpeed, puppet.Action.MaxNormalSpeed);
                        WriteBigEndianFloat(slotData, GameMemoryAddresses.PuppetSync.SlotOffset_StickDistance,  puppet.Action.StickDistance);
                        WriteBigEndianU32  (slotData, GameMemoryAddresses.PuppetSync.SlotOffset_ModeFlg,        puppet.Action.ModeFlg);
                        WriteBigEndianU32  (slotData, GameMemoryAddresses.PuppetSync.SlotOffset_NoResetFlg0,    puppet.Action.NoResetFlg0);
                        WriteBigEndianU32  (slotData, GameMemoryAddresses.PuppetSync.SlotOffset_NoResetFlg1,    puppet.Action.NoResetFlg1);

                        // v3 held items: aim angles and what they carry (puppet_held.c).
                        WriteBigEndianS16(slotData, GameMemoryAddresses.PuppetSync.SlotOffset_BodyAngleX, puppet.Action.BodyAngleX);
                        WriteBigEndianS16(slotData, GameMemoryAddresses.PuppetSync.SlotOffset_BodyAngleY, puppet.Action.BodyAngleY);
                        byte grabKind = HeldItemState.SanitizeGrabKind(puppet.Equipment.GrabKind);
                        slotData[GameMemoryAddresses.PuppetSync.SlotOffset_GrabKind] = grabKind;
                        slotData[GameMemoryAddresses.PuppetSync.SlotOffset_GrabFuse] =
                            grabKind == PuppetLayout.PUPPET_GRAB_KIND_BOMB ? puppet.Equipment.GrabFuse : (byte)0;

                        slotBoat = puppet.Boat;
                        _anmEntries[i] = AnimEntry(i, puppet, slotData[GameMemoryAddresses.PuppetSync.SlotOffset_CurProc]);
                    }
                }

                _dolphin.WriteMemory(slotBase, slotData);
                WriteBoat(i, slotBoat);
            }

            // Other players' anims for the procs their puppets have no init for (after the slots: the REL
            // drops a sample whose proc isn't the slot's yet).
            PublishAnimMirror();

            // The hook keeps slots 0..desiredCount-1 spawned, so ask for everything up to the
            // highest active slot; lower inactive slots are hidden by puppet_draw (active == 0).
            // The shared world's live despawn runs in a puppet's tick: with no peer puppet here
            // and placed items still to despawn, ask for slot 0 anyway — left inactive, the hook
            // spawns it PARKED (invisible, no collision) just to run the scan. Same for the local
            // Link's outfit/colour (applied by the REL), until it shows the local player's choice, and
            // for the live world (LiveWorldPoke) while the REL has a batch of other players' bits to handle, and for
            // the sea chart while another player is on the sea with us.
            // (Non-short-circuit | : every check updates its own state every tick.)
            // The other players on the sea chart (SeaChartMarkers): everyone on the sea, visible or not. The chart
            // calls into the REL while the menu is open, so keep one loaded while they're on the sea with us.
            var chartPlayers = _remotePuppets
                .Where(kv => !_lastPuppetReceive.TryGetValue(kv.Key, out var rx) || (now - rx).TotalMilliseconds < StaleDataMillis)
                .Select(kv => kv.Value).ToList();
            bool chartNeedsRel = SeaChartMarkers.Publish(_dolphin, localStage, chartPlayers);

            int desiredCount = highestActiveSlot + 1;
            if (_despawnWorker.IsNeeded(_dolphin) | _liveWorld.IsNeeded(_dolphin) | LocalLookNeedsRel(linkPtr!.Value, localLook) | chartNeedsRel)
                desiredCount = Math.Max(desiredCount, 1);
            WriteSyncHeader(GameMemoryAddresses.PuppetSync.Magic, desiredCount);

            // Diagnostic: mirror slot 0's visibility-chain bitmap to scratch so the UI
            // can show which gate is dropping (CLIENT_DBG_SLOT0_GATES in puppet_shared.h).
            WriteBigEndianU32(_u32Buf, 0, slot0Diag);
            _dolphin.WriteMemory(GameMemoryAddresses.PuppetSync.ClientDbgSlot0GatesAddr, _u32Buf);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error writing puppet sync data to memory");
        }
    }

    private static uint ReadBigEndianU32(byte[] bytes, int offset)
    {
        return ((uint)bytes[offset] << 24) |
               ((uint)bytes[offset + 1] << 16) |
               ((uint)bytes[offset + 2] << 8) |
               bytes[offset + 3];
    }

    /// <summary>
    /// A slot's anim mirror entry for <paramref name="puppet"/> (<see cref="AnimMirror.Encode"/>), with the
    /// slot's proc. Its SEQ moves on only when a new sample arrived (a new PuppetData object).
    /// </summary>
    private byte[] AnimEntry(int slot, PuppetData puppet, byte proc)
    {
        if (!ReferenceEquals(_anmSamples[slot], puppet))
        {
            _anmSamples[slot] = puppet;
            if (++_anmSeqs[slot] == 0)
                _anmSeqs[slot] = 1;
        }
        return AnimMirror.Encode(puppet.Animation, proc, _anmSeqs[slot]);
    }

    /// <summary>Write the slots' anim mirror entries; a no-op until the REL has made the block.</summary>
    private void PublishAnimMirror()
    {
        uint block = AnimMirror.Publish(_dolphin, _anmEntries) ?? 0;
        if (block != _anmBlock)
        {
            Logger.Information(block != 0 ? "[puppet] anim mirror block at 0x{Block:X8}" : "[puppet] anim mirror block gone (was 0x{Old:X8})",
                               block != 0 ? block : _anmBlock);
            _anmBlock = block;
        }
    }

    /// <summary>
    /// Does the local Link need the puppet REL to show <paramref name="want"/>? The REL only runs while
    /// a puppet exists, so with no peer puppet here the caller asks the hook for a parked one (like the
    /// world-sync despawn) until the local Link matches. Logs each change of verdict.
    /// </summary>
    private bool LocalLookNeedsRel(uint linkPtr, AppearanceState want)
    {
        if (Services.LocalAppearance.Read(_dolphin, linkPtr) is not { } look)
            return false;
        bool needs = Services.LocalAppearance.NeedsRel(want, look);
        if (needs != _localLookNeedsRel)
        {
            _localLookNeedsRel = needs;
            var status = _dolphin.ReadMemory(PuppetLayout.LOCAL_APPEARANCE_STATUS_ADDR, 4);
            Logger.Information(
                "[appearance] local look {State}: want clothes={Clothes} rgb={R},{G},{B}; game casual={Casual} heroClothes={Hero} clear={Clear} " +
                "header=0x{Header:X8} relImage=0x{Image:X8} relRgb=0x{Applied:X6} relStatus=0x{Status:X}",
                needs ? "needs the REL (keeping a puppet)" : "matches",
                want.ClothesType, want.ColorR, want.ColorG, want.ColorB, look.LinkCasual, look.HeroClothesOwned, look.ClearCount,
                look.HeaderImage, look.PublishedImage, look.AppliedRgb,
                status is { Length: 4 } ? ReadBigEndianU32(status, 0) : 0u);
        }
        return needs;
    }

    /// <summary>
    /// Read the local player's state from game memory for broadcasting to other players.
    /// </summary>
    public PuppetData? ReadLocalPlayerState()
    {
        if (!_dolphin.IsConnected)
            return null;

        try
        {
            // Read Link actor pointer
            var linkPtr = _dolphin.Read(GameMemoryAddresses.Player.LinkActorPointer);
            if (linkPtr == null || linkPtr == 0)
                return null;

            uint actorBase = linkPtr.Value;

            // Read position (fopAc_ac_c.current.pos at offset 0x1F8)
            var posX = _dolphin.ReadWithOffset(actorBase, GameMemoryAddresses.Player.LinkPositionXOffset);
            var posY = _dolphin.ReadWithOffset(actorBase, GameMemoryAddresses.Player.LinkPositionYOffset);
            var posZ = _dolphin.ReadWithOffset(actorBase, GameMemoryAddresses.Player.LinkPositionZOffset);

            // Read rotation Y (shape_angle.y at offset 0x206)
            var rotY = _dolphin.ReadWithOffset(actorBase, GameMemoryAddresses.Player.LinkRotationYOffset);

            // daPy_lk_c field offsets all come from puppet_shared.h (PuppetLayout.DAPY_OFF_*).
            float speedF          = ReadBigEndianFloat(actorBase + PuppetLayout.DAPY_OFF_SPEED_F);
            float mMaxNormalSpeed = ReadBigEndianFloat(actorBase + PuppetLayout.DAPY_OFF_MAX_NORMAL_SPEED);
            float mStickDistance  = ReadBigEndianFloat(actorBase + PuppetLayout.DAPY_OFF_STICK_DISTANCE);
            float mVelocity       = ReadBigEndianFloat(actorBase + PuppetLayout.DAPY_OFF_VELOCITY);
            uint  mModeFlg        = ReadBigEndianU32FromAddress(actorBase + PuppetLayout.DAPY_OFF_MODE_FLG);
            uint  mNoResetFlg0    = ReadBigEndianU32FromAddress(actorBase + PuppetLayout.DAPY_OFF_NO_RESET_FLG0);
            uint  mNoResetFlg1    = ReadBigEndianU32FromAddress(actorBase + PuppetLayout.DAPY_OFF_NO_RESET_FLG1);
            ushort mEquipItem     = ReadBigEndianU16FromAddress(actorBase + PuppetLayout.DAPY_OFF_EQUIP_ITEM);
            short mStickAngle     = (short)ReadBigEndianU16FromAddress(actorBase + PuppetLayout.DAPY_OFF_STICK_ANGLE);
            ushort upperAnmIdx    = ReadBigEndianU16FromAddress(actorBase + PuppetLayout.DAPY_OFF_UPPER_ANM_IDX);

            // Current proc (mCurProc) — the full daPyProc enum value, broadcast raw so the
            // puppet can pick the matching proc*_init.
            byte curProc = 0x04; // Default to WAIT_e
            var procBytes = _dolphin.ReadMemory(actorBase + PuppetLayout.DAPY_OFF_CUR_PROC, 4);
            if (procBytes != null && procBytes.Length >= 4)
            {
                uint procId = ReadBigEndianU32(procBytes, 0);
                curProc = (byte)(procId & 0xFF);
            }

            // While a draw / put-away anim (REST, TAKE*) plays, report the item Link WILL hold, so the
            // puppet starts its own draw / put-away at the same time instead of a whole anim + a tick late.
            if (HeldItemState.IsEquipAnim(upperAnmIdx))
                mEquipItem = HeldItemState.ReportedItem(mEquipItem,
                    ReadBigEndianU16FromAddress(actorBase + PuppetLayout.DAPY_OFF_NEXT_EQUIP_ITEM), upperAnmIdx);
            short bodyAngleX = (short)ReadBigEndianU16FromAddress(actorBase + PuppetLayout.DAPY_OFF_BODY_ANGLE_X);
            short bodyAngleY = (short)ReadBigEndianU16FromAddress(actorBase + PuppetLayout.DAPY_OFF_BODY_ANGLE_Y);
            byte grabKind = HeldItemState.ReadGrabKind(_dolphin, actorBase);
            byte grabFuse = grabKind == PuppetLayout.PUPPET_GRAB_KIND_BOMB ? HeldItemState.ReadGrabFuse(_dolphin, actorBase) : (byte)0;
            if (_lastLoggedHeld != (mEquipItem, grabKind))
            {
                Logger.Information("[held] local item 0x{Old:X} -> 0x{New:X} carry {OldGrab} -> {NewGrab} (upper anim 0x{Upper:X}, proc 0x{Proc:X2})",
                    _lastLoggedHeld.Item, mEquipItem, _lastLoggedHeld.Grab, grabKind, upperAnmIdx, curProc);
                _lastLoggedHeld = (mEquipItem, grabKind);
            }

            // Proc (re)start detection → ProcSeq. Combo swings re-init the SAME proc, which is only
            // visible as the lower-body anim frame jumping back or the combo counter ticking.
            float underFrame = ReadBigEndianFloat(actorBase + PuppetLayout.DAPY_OFF_UNDER_FRAME);
            var comboBytes = _dolphin.ReadMemory(actorBase + PuppetLayout.DAPY_OFF_CUT_COMBO, 1);
            byte combo = comboBytes?[0] ?? _lastCombo;
            if (curProc != _lastProc || underFrame < _lastUnderFrame - 0.5f || (byte)(combo - _lastCombo) == 1)
                _procSeq++;
            _lastProc = curProc;
            _lastUnderFrame = underFrame;
            _lastCombo = combo;

            bool guarding = curProc is PuppetLayout.DAPY_PROC_GUARD_0 or PuppetLayout.DAPY_PROC_GUARD_1 or PuppetLayout.DAPY_PROC_GUARD_2
                            || upperAnmIdx is PuppetLayout.DAPY_UPPER_ANM_GUARD_0 or PuppetLayout.DAPY_UPPER_ANM_GUARD_1;

            // Equipment: the REL swaps a peer's ids into these bytes while it runs their puppet —
            // keep the last value read outside a swap.
            if (EquipSwapGuard.ReadEquipped(_dolphin) is { } equipped)
                (_lastSword, _lastShield) = equipped;

            // Read current stage name and room number for visibility filtering
            var stageBytes = _dolphin.ReadMemory(GameMemoryAddresses.Stage.CurrentStageName.Address, 8);
            string stageName = "";
            if (stageBytes != null)
            {
                int nullIdx = Array.IndexOf(stageBytes, (byte)0);
                int len = nullIdx >= 0 ? nullIdx : stageBytes.Length;
                stageName = System.Text.Encoding.ASCII.GetString(stageBytes, 0, len);
            }
            var roomNumber = _dolphin.Read(GameMemoryAddresses.Stage.CurrentRoomNumber);

            if (posX == null || posY == null || posZ == null)
                return null;

            var appearance = LocalAppearance;
            LocalWarp = _warpReader.Read(_dolphin, stageName);

            var data = new PuppetData
            {
                Position = new Vector3(posX.Value, posY.Value, posZ.Value),
                Rotation = rotY ?? 0,
                Animation = new AnimationState
                {
                    AnimationSpeed = speedF > 0.5f ? 1.0f : 0.0f,
                    // m_anm_heap_upper[UPPER_MOVE2].mIdx (0xFFFF = none): the puppet mirrors the
                    // item ones (aim holds, throws, bow draw, carry) and the draw / put-away timing.
                    UpperBodyAnimation = upperAnmIdx,
                },
                Action = new ActionState
                {
                    StateFlags = speedF > 0.5f ? 1u : 0u,
                    Velocity = mVelocity,       // full peer-Link blend input (0x35BC)
                    CurProc = curProc,
                    SpeedF = speedF,
                    MaxNormalSpeed = mMaxNormalSpeed,
                    StickDistance = mStickDistance,
                    ModeFlg = mModeFlg,
                    NoResetFlg0 = mNoResetFlg0,
                    NoResetFlg1 = mNoResetFlg1,
                    StickAngle = mStickAngle,
                    ProcSeq = _procSeq,
                    BodyAngleX = bodyAngleX,
                    BodyAngleY = bodyAngleY,
                    ActionFlags = guarding ? (byte)PuppetLayout.PUPPET_ACTION_FLAG_GUARD : (byte)0,
                },
                Equipment = new EquipmentState
                {
                    SwordId = _lastSword,
                    ShieldId = _lastShield,
                    ItemInHand = mEquipItem,
                    GrabKind = grabKind,
                    GrabFuse = grabFuse,
                },
                Appearance = new AppearanceState
                {
                    ClothesType = appearance.ClothesType,
                    ColorR = appearance.ColorR,
                    ColorG = appearance.ColorG,
                    ColorB = appearance.ColorB,
                    BoatR = appearance.BoatR,
                    BoatG = appearance.BoatG,
                    BoatB = appearance.BoatB,
                },
                StageName = stageName,
                RoomNumber = roomNumber ?? 0,
                Timestamp = DateTime.UtcNow,
                Boat = ReadLocalBoat(curProc, stageName),
                Warp = LocalWarp,
            };
            // Body / face anims for the procs other players' RELs have no init for (puppet_anmmirror.c).
            AnimMirror.ReadLocal(_dolphin, actorBase, data.Animation);

            return data;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error reading local player state");
            return null;
        }
    }

    /// <summary>
    /// The local King of Red Lions while Link rides it, else null. Riding is the SHIP_RIDE status
    /// bit OR a ship proc: the REL zeroes the status words while a puppet executes (puppet_execute.c
    /// guard), so a status read can land mid-puppet and miss the bit.
    /// </summary>
    private BoatState? ReadLocalBoat(byte curProc, string stageName)
    {
        uint status0 = ReadBigEndianU32FromAddress(GameMemoryAddresses.Player.PlayerStatusBitfield0.Address);
        bool riding = (status0 & GameMemoryAddresses.Sea.PlayerStatus0ShipRide) != 0
                      || curProc is >= GameMemoryAddresses.Sea.ProcShipFirst and <= GameMemoryAddresses.Sea.ProcShipLast
                      || curProc == GameMemoryAddresses.Sea.ProcDemoShipSit;
        // Not aboard: the boat left on the Great Sea still shows, parked. Only on the sea stage, and only while the
        // ship pointer is a live daShip_c (play.mpPlayerPtr[2] isn't cleared when a stage without the boat loads).
        if (!riding && stageName != GameMemoryAddresses.Sea.SeaStageName)
            return null;

        uint ship = ReadPointer(GameMemoryAddresses.Sea.ShipActorPtr);
        if (ship == 0)
            return null;
        if (!riding && ReadBigEndianU16FromAddress(ship + GameMemoryAddresses.Sea.ActorOffsetProcName) != GameMemoryAddresses.Sea.ProcNameShip)
            return null;

        var boat = new BoatState
        {
            Position = new Vector3(
                ReadBigEndianFloat(ship + GameMemoryAddresses.Sea.ShipOffsetPosX),
                ReadBigEndianFloat(ship + GameMemoryAddresses.Sea.ShipOffsetPosX + 4),
                ReadBigEndianFloat(ship + GameMemoryAddresses.Sea.ShipOffsetPosX + 8)),
            Rotation = (short)ReadBigEndianU16FromAddress(ship + GameMemoryAddresses.Sea.ShipOffsetRotY),
            SpeedF = ReadBigEndianFloat(ship + GameMemoryAddresses.Sea.ShipOffsetSpeedF),
            Flying = riding && (ReadBigEndianU32FromAddress(ship + GameMemoryAddresses.Sea.ShipOffsetStateFlag)
                                & GameMemoryAddresses.Sea.ShipStateFly) != 0,
            Parked = !riding,
        };
        if (boat.Parked) boat.SpeedF = 0; // drifting to a stop: no prediction on the receiver
        ReadLocalBoatPose(ship, boat);
        return boat.IsValid() ? boat : null;
    }

    /// <summary>First and one-past-last daShip_c pose field (mPart .. m03E8), read as one block.</summary>
    public const uint ShipPoseStart = GameMemoryAddresses.Sea.ShipOffsetPart;
    public const int ShipPoseLength = (int)(GameMemoryAddresses.Sea.ShipOffsetMastScale + 4 - ShipPoseStart);

    /// <summary>
    /// The sail, tiller, mast, head, cannon and crane pose daShip_c draws with (d_a_ship.cpp:110-231,
    /// 304-317); an unreadable block leaves the pose at rest.
    /// </summary>
    private void ReadLocalBoatPose(uint ship, BoatState boat)
    {
        byte[]? pose = _dolphin.ReadMemory(ship + ShipPoseStart, ShipPoseLength);
        if (pose == null || pose.Length < ShipPoseLength)
            return;

        ParseShipPose(pose, boat);
        boat.MastFrame = ReadMorfFrame(ship + GameMemoryAddresses.Sea.ShipOffsetBodyAnm);
        boat.HeadFrame = ReadMorfFrame(ship + GameMemoryAddresses.Sea.ShipOffsetHeadAnm);
    }

    /// <summary>
    /// The pose fields of a daShip_c block read from <see cref="ShipPoseStart"/>. Angles are clamped
    /// into the ranges the game keeps them in (BoatState limits) so a surprise value never gets the
    /// whole puppet rejected.
    /// </summary>
    public static void ParseShipPose(byte[] pose, BoatState boat)
    {
        if (pose.Length < ShipPoseLength)
            return;
        short S16(uint offset) => BinaryPrimitives.ReadInt16BigEndian(pose.AsSpan((int)(offset - ShipPoseStart)));

        boat.SailAngle = Math.Clamp(S16(GameMemoryAddresses.Sea.ShipOffsetSailAngle), (short)-BoatState.MaxSailAngle, BoatState.MaxSailAngle);
        boat.Tiller = Math.Clamp(S16(GameMemoryAddresses.Sea.ShipOffsetTiller), (short)-BoatState.MaxTiller, BoatState.MaxTiller);
        boat.HeadX = Math.Clamp(S16(GameMemoryAddresses.Sea.ShipOffsetHeadX), BoatState.MinHeadX, BoatState.MaxHeadX);
        boat.HeadY = Math.Clamp(S16(GameMemoryAddresses.Sea.ShipOffsetHeadY), (short)-BoatState.MaxHeadY, BoatState.MaxHeadY);
        boat.MastRaised = S16(GameMemoryAddresses.Sea.ShipOffsetMastBck) == PuppetLayout.SHIP_BCK_MAST_ON2;
        int headBck = S16(GameMemoryAddresses.Sea.ShipOffsetHeadBck);
        boat.HeadBck = BoatState.IsHeadBck(headBck) ? (byte)headBck : (byte)0;
        // m03E8 is 1.0 or 0.001 (d_a_ship.cpp:1334-1342, 4555-4559).
        float mastScale = BitConverter.Int32BitsToSingle(
            BinaryPrimitives.ReadInt32BigEndian(pose.AsSpan((int)(GameMemoryAddresses.Sea.ShipOffsetMastScale - ShipPoseStart))));
        boat.MastHidden = mastScale < 0.5f;

        // Only the part that's out uses its angles; the others stay 0.
        byte part = pose[GameMemoryAddresses.Sea.ShipOffsetPart - ShipPoseStart];
        boat.Part = part <= BoatState.PartCrane ? part : BoatState.PartWait;
        if (boat.Part == BoatState.PartCannon)
        {
            boat.CannonYaw = S16(GameMemoryAddresses.Sea.ShipOffsetCannonYaw);
            boat.CannonPitch = Math.Clamp(S16(GameMemoryAddresses.Sea.ShipOffsetCannonPitch), (short)0, BoatState.MaxCannonPitch);
        }
        else if (boat.Part == BoatState.PartCrane)
        {
            // s16 sum, as craneJointCallBack adds them (d_a_ship.cpp:175).
            int crane = (short)(S16(GameMemoryAddresses.Sea.ShipOffsetCraneAngle) + S16(GameMemoryAddresses.Sea.ShipOffsetCraneSwing));
            boat.CraneAngle = (short)Math.Clamp(crane, -BoatState.MaxCraneAngle, BoatState.MaxCraneAngle);
            boat.RopeLength = (byte)Math.Clamp((int)S16(GameMemoryAddresses.Sea.ShipOffsetRopeCnt), 0, BoatState.MaxRopeLength);
        }
    }

    /// <summary>A daShip_c morf's bck frame, rounded to whole frames (0 if the morf or frame is
    /// unreadable). Rounded, not truncated: a finished bck sits at end - 0.001 (J3DFrameCtrl::update
    /// EMode_NONE), which must arrive as the end frame, not the one before it.</summary>
    private byte ReadMorfFrame(uint morfPtrAddress)
    {
        uint morf = ReadPointer(morfPtrAddress);
        if (morf == 0)
            return 0;
        float frame = ReadBigEndianFloat(morf + GameMemoryAddresses.Sea.McaMorfOffsetFrame);
        return float.IsFinite(frame) ? (byte)Math.Clamp(MathF.Round(frame), 0f, byte.MaxValue) : (byte)0;
    }

    // === Slot helpers ===

    /// <summary>The local stage, room and position (x, z) as of the last 20Hz tick.</summary>
    public (string Stage, byte Room, float X, float Z) LocalLocation => (_lastLocalStage, _lastLocalRoom, _lastLocalX, _lastLocalZ);

    /// <summary>
    /// The puppet slot assigned to <paramref name="playerId"/>, shown here or not (their projectile
    /// events are keyed by it; PlayerEventService checks where the event happened).
    /// </summary>
    public bool TryGetSlot(string playerId, out int slot)
    {
        lock (_slotLock)
            return _slotAssignments.TryGetValue(playerId, out slot);
    }

    /// <summary>Returns the player ID assigned to the given slot, or null if empty.</summary>
    private string? GetPlayerInSlot(int slot)
    {
        lock (_slotLock)
        {
            foreach (var kvp in _slotAssignments)
            {
                if (kvp.Value == slot)
                    return kvp.Key;
            }
            return null;
        }
    }

    // === Big-endian write helpers (GameCube is big-endian) ===

    private static void WriteBigEndianU32(byte[] buffer, int offset, uint value)
    {
        buffer[offset + 0] = (byte)((value >> 24) & 0xFF);
        buffer[offset + 1] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 3] = (byte)(value & 0xFF);
    }

    private static void WriteBigEndianU16(byte[] buffer, int offset, ushort value)
    {
        buffer[offset + 0] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 1] = (byte)(value & 0xFF);
    }

    private static void WriteBigEndianS16(byte[] buffer, int offset, short value)
    {
        WriteBigEndianU16(buffer, offset, (ushort)value);
    }

    private static void WriteBigEndianFloat(byte[] buffer, int offset, float value)
    {
        uint bits = BitConverter.SingleToUInt32Bits(value);
        buffer[offset + 0] = (byte)((bits >> 24) & 0xFF);
        buffer[offset + 1] = (byte)((bits >> 16) & 0xFF);
        buffer[offset + 2] = (byte)((bits >> 8) & 0xFF);
        buffer[offset + 3] = (byte)(bits & 0xFF);
    }

    private float ReadBigEndianFloat(uint address)
    {
        byte[]? bytes = _dolphin.ReadMemory(address, 4);
        if (bytes == null || bytes.Length < 4)
            return 0f;
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToSingle(bytes, 0);
    }

    /// <summary>
    /// Read a big-endian u32 from an absolute GameCube memory address.
    /// </summary>
    private uint ReadBigEndianU32FromAddress(uint address)
    {
        byte[]? bytes = _dolphin.ReadMemory(address, 4);
        return bytes == null ? 0u : ReadBigEndianU32(bytes, 0);
    }

    /// <summary>Read a big-endian u16 from an absolute GameCube memory address.</summary>
    private ushort ReadBigEndianU16FromAddress(uint address)
    {
        byte[]? bytes = _dolphin.ReadMemory(address, 2);
        return bytes == null || bytes.Length < 2 ? (ushort)0 : (ushort)(bytes[0] << 8 | bytes[1]);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            StopBroadcast();
            Stop();
            _disposed = true;
        }
    }
}
