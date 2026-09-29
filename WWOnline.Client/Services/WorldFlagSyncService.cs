using System.Collections.Concurrent;
using Serilog;
using WWOnline.Data;
using WWOnline.Hubs;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>
/// Shared-world sync, layer 1: per-stage save flags (chests, memory switches, collected placed
/// items, visited rooms, dungeon items) are OR-merged across all players via the server.
///
/// Every tick (4Hz, only while the scene is stable):
///   local[slot]  = saved flags for every stage slot, with the current stage's LIVE copy ORed in
///   outbound     = bits in local that the server hasn't taken from us nor sent us → send the slot
///                  (marked sent only once the send succeeded; one send per slot in flight)
///   inbound      = bits we've received that the game doesn't have → OR them into memory
/// Inbound is enforced every tick rather than applied once, so a stage load/save that
/// overwrites memory mid-transition just gets re-applied on the next stable tick — except
/// memory switches (see <see cref="SwitchApplyGuard"/>).
/// On every (re)connect the per-connection state is dropped, the server's snapshot is pulled and
/// everything this game has is re-sent (the server may have restarted, or we attached offline).
///
/// Layer 2: item bits applied to the CURRENT stage are also ORed into the remote-item mask
/// (scratch memory) so the puppet REL can despawn those placed items live.
/// Live world: chest and switch bits applied to the current stage go to <see cref="LiveWorldPoke"/>,
/// which hands them to the REL so actors that read their flag only at create catch up live.
/// </summary>
public class WorldFlagSyncService : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<WorldFlagSyncService>();

    private const int TickMs = 250;
    private const int StableTicksRequired = 4; // 1s of the same stage with Link present

    private readonly IDolphinService _dolphin;
    private readonly SignalRClientService _signalR;
    private readonly RoomSettingsService _room;
    private readonly DespawnWorker _despawnWorker;
    private readonly LiveWorldPoke _liveWorld;

    // Guarded by _tickLock. _sent / _received / _sending are per connection (reset on Connected).
    private StageFlags[] _sent = NewSlots();
    private StageFlags[] _received = NewSlots();
    private readonly bool[] _sending = new bool[StageFlags.SlotCount];
    private int _connectionGeneration;
    private readonly SwitchApplyGuard _switchGuard = new();
    private readonly ConcurrentQueue<StageFlags> _inbox = new();
    private readonly object _tickLock = new();
    private System.Timers.Timer? _timer;

    private readonly SceneStabilityGate _scene = new(StableTicksRequired);
    private int? _lastSlot;
    private uint _remoteItemMask;

    public WorldFlagSyncService(IDolphinService dolphin, SignalRClientService signalR, RoomSettingsService room,
        DespawnWorker despawnWorker, LiveWorldPoke liveWorld)
    {
        _dolphin = dolphin;
        _signalR = signalR;
        _room = room;
        _despawnWorker = despawnWorker;
        _liveWorld = liveWorld;
    }

    public void Start()
    {
        if (_timer != null) return;
        Logger.Information("[world] shared-world flag sync started");

        _signalR.StageFlagsReceived += OnStageFlagsReceived;
        _signalR.Connected += OnConnected;
        _ = FetchSnapshotAsync(); // no-op while offline: Connected fetches it

        _timer = new System.Timers.Timer(TickMs) { AutoReset = true };
        _timer.Elapsed += (_, _) =>
        {
            // Skip rather than overlap a slow tick.
            if (!Monitor.TryEnter(_tickLock)) return;
            try { Tick(); }
            catch (Exception ex) { Logger.Error(ex, "[world] tick failed"); }
            finally { Monitor.Exit(_tickLock); }
        };
        _timer.Start();
    }

    public void Stop()
    {
        if (_timer == null) return;
        _signalR.StageFlagsReceived -= OnStageFlagsReceived;
        _signalR.Connected -= OnConnected;
        _timer.Stop();
        _timer.Dispose();
        _timer = null;
        Logger.Information("[world] shared-world flag sync stopped");
    }

    private void OnStageFlagsReceived(StageFlags flags)
    {
        if (flags.IsValid()) _inbox.Enqueue(flags);
    }

    // New connection (first connect, manual reconnect or auto-reconnect): the server may have
    // restarted (empty world) or others may have added bits while we were away. Start this
    // connection's state from scratch, re-pull the snapshot, and re-send everything we have.
    private void OnConnected()
    {
        lock (_tickLock)
        {
            _connectionGeneration++;
            _sent = NewSlots();
            _received = NewSlots();
            Array.Clear(_sending);
            _inbox.Clear();
        }
        _ = FetchSnapshotAsync();
    }

    private async Task FetchSnapshotAsync()
    {
        try
        {
            var snapshot = await _signalR.GetWorldFlagsAsync();
            if (snapshot == null) return;
            foreach (var flags in snapshot.Where(f => f.IsValid()))
                _inbox.Enqueue(flags);
            Logger.Information("[world] received shared-world snapshot: {Count} stage slot(s)", snapshot.Count);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "[world] failed to fetch shared-world snapshot");
        }
    }

    private void Tick()
    {
        if (!_dolphin.IsConnected) return;

        while (_inbox.TryDequeue(out var incoming))
            _received[incoming.Slot].MergeFrom(incoming);

        // Room rule off: neither share nor apply. Local bits set meanwhile are sent when it's back on.
        if (!_room.Current.SharedWorld)
        {
            _liveWorld.SetStage(null); // nothing for the REL to catch up with
            return;
        }

        if (!_scene.Check(_dolphin)) return;

        LogRelDespawns();
        bool online = _signalR.IsConnected;
        int? slot = ReadCurrentSlot();
        if (slot != _lastSlot)
        {
            // New stage: the REL's despawn mask is per-stage.
            _remoteItemMask = 0;
            WriteRemoteItemMask(0, slot ?? 0);
            Logger.Information("[world] current stage slot {Old} -> {New}", _lastSlot, slot);
            _lastSlot = slot;
        }
        _liveWorld.SetStage(slot);

        var savedBytes = _dolphin.ReadMemory(GameMemoryAddresses.WorldFlags.SavedMemoryBase,
            StageFlags.SlotCount * GameMemoryAddresses.WorldFlags.MemorySize);
        if (savedBytes == null) return;
        var liveBytes = slot.HasValue
            ? _dolphin.ReadMemory(GameMemoryAddresses.WorldFlags.LiveMemory, GameMemoryAddresses.WorldFlags.MemorySize)
            : null;

        for (int i = 0; i < StageFlags.SlotCount; i++)
        {
            var local = Parse(savedBytes, i * GameMemoryAddresses.WorldFlags.MemorySize, i);
            if (i == slot && liveBytes != null)
                local.MergeFrom(Parse(liveBytes, 0, i));

            // Outbound: bits this game set that the shared world hasn't seen from us or anyone.
            if (online && !_sending[i])
            {
                var known = _sent[i].Clone();
                known.MergeFrom(_received[i]);
                var fresh = local.Except(known);
                if (!fresh.IsEmpty) Send(local, fresh.BitCount);
            }

            // Inbound: shared-world bits this game is missing.
            var missing = _received[i].Except(local);
            _switchGuard.Filter(i, local, missing);
            if (missing.IsEmpty) continue;

            if (i == slot && liveBytes != null)
            {
                // Current stage: the live copy is authoritative (putSave overwrites mSave[slot]
                // from it when we leave), so only write there.
                ApplyBits(GameMemoryAddresses.WorldFlags.LiveMemory, missing);
                if (missing.Item != 0)
                {
                    _remoteItemMask |= missing.Item;
                    WriteRemoteItemMask(_remoteItemMask, slot.Value);
                }
                // Chests / switches that read their flag only at create: the REL catches them up
                // once the next tick reads these bits back from the live copy.
                _liveWorld.AddRemote(i, LiveWorldPoke.FromMemBit(missing.Tbox, missing.Switch));
            }
            else
            {
                ApplyBits(GameMemoryAddresses.WorldFlags.SavedSlot(i), missing);
            }
            Logger.Information("[world] applied {Bits} shared bit(s) to slot {Slot}{Live}: {Flags}",
                missing.BitCount, i, i == slot ? " (current stage, live)" : "", missing);
        }

        if (slot.HasValue && liveBytes != null)
            TickLiveWorld(Parse(liveBytes, 0, slot.Value));
    }

    /// <summary>
    /// Hand the REL the chest / switch bits other players set here that the live copy now has (read back
    /// this tick, before this tick's writes), and log what it acknowledged.
    /// </summary>
    private void TickLiveWorld(StageFlags live)
    {
        if (_liveWorld.Tick(_dolphin, LiveWorldPoke.FromMemBit(live.Tbox, live.Switch)) is not { } r) return;
        if (r.Acknowledged is { IsEmpty: false } done)
            Logger.Information("[world] live world: REL handled {Batch} ({Pokes} actor(s) updated so far)", done, r.PokeCount);
        if (r.Published is { IsEmpty: false } batch)
            Logger.Information("[world] live world: handing the REL {Batch}", batch);
    }

    /// <summary>
    /// Send one slot's flags. They're marked sent only once the server has them, so a send made
    /// while offline (or one that fails) is retried on a later tick. Called under _tickLock.
    /// </summary>
    private void Send(StageFlags local, int freshBits)
    {
        int slot = local.Slot, generation = _connectionGeneration;
        var toSend = local.Clone();
        _sending[slot] = true;
        Logger.Information("[world] local +{Bits} bit(s) → sending {Flags}", freshBits, toSend);
        _ = Task.Run(async () =>
        {
            bool sent = false;
            try { sent = await _signalR.SendStageFlagsAsync(toSend); }
            catch (Exception ex) { Logger.Warning(ex, "[world] send failed for slot {Slot}", slot); }
            lock (_tickLock)
            {
                if (generation != _connectionGeneration) return; // reconnected meanwhile: state was reset
                _sending[slot] = false;
                if (sent) _sent[slot].MergeFrom(toSend);
            }
        });
    }

    /// <summary>
    /// The current stage's save slot (dStage_stagInfo_GetSaveTbl), or null if unknown.
    /// Without it the live copy isn't synced (saved slots still are).
    /// </summary>
    private int? ReadCurrentSlot()
    {
        var ptrBytes = _dolphin.ReadMemory(GameMemoryAddresses.WorldFlags.StagInfoPtr, 4);
        if (ptrBytes == null) return null;
        uint p = ReadU32(ptrBytes, 0);
        if (p < 0x80000000 || p >= 0x84000000) return null;
        var b = _dolphin.ReadMemory(p + GameMemoryAddresses.WorldFlags.StagSaveTblOffset, 1);
        if (b == null) return null;
        int slot = (b[0] >> 1) & 0x7F;
        return slot < StageFlags.SlotCount ? slot : null;
    }

    /// <summary>
    /// Publish the item bits other players collected in the current stage for the REL's live
    /// despawn (puppet_worldsync.c). Tag is cleared first and set last so the REL never pairs a
    /// new mask with an old stage (it re-checks the tag after reading the mask).
    /// </summary>
    private void WriteRemoteItemMask(uint mask, int slot)
    {
        _despawnWorker.MarkChanged(_dolphin); // the REL must rescan before its result counts again
        WriteU32(PuppetLayout.WORLDSYNC_STAGE_TAG_ADDR, 0);
        WriteU32(PuppetLayout.WORLDSYNC_ITEM_MASK_ADDR, mask);
        if (mask != 0)
            WriteU32(PuppetLayout.WORLDSYNC_STAGE_TAG_ADDR, (uint)PuppetLayout.WORLDSYNC_TAG_MAGIC | (uint)slot);
    }

    private uint _lastDespawnCount;

    /// <summary>Surface the REL's live despawns (layer 2) in the client log.</summary>
    private void LogRelDespawns()
    {
        var c = _dolphin.ReadMemory(PuppetLayout.WORLDSYNC_DESPAWN_COUNT_ADDR, 8);
        if (c == null) return;
        uint count = ReadU32(c, 0), last = ReadU32(c, 4);
        if (count == _lastDespawnCount) return;
        Logger.Information("[world] REL despawned {N} placed item(s); last: slot {Slot} item 0x{Item:X2} bit {Bit}",
            count - _lastDespawnCount, last >> 16, (last >> 8) & 0xFF, last & 0xFF);
        _lastDespawnCount = count;
    }

    private void WriteU32(uint addr, uint v) =>
        _dolphin.WriteMemory(addr, new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v });

    /// <summary>OR <paramref name="bits"/> into a dSv_memory_c at <paramref name="baseAddr"/>, one word at a
    /// time (read-modify-write per word keeps the window where the game could race us tiny).</summary>
    private void ApplyBits(uint baseAddr, StageFlags bits)
    {
        OrU32(baseAddr + GameMemoryAddresses.WorldFlags.OffTbox, bits.Tbox);
        for (int w = 0; w < StageFlags.SwitchWords; w++)
            OrU32(baseAddr + GameMemoryAddresses.WorldFlags.OffSwitch + (uint)(w * 4), bits.Switch[w]);
        OrU32(baseAddr + GameMemoryAddresses.WorldFlags.OffItem, bits.Item);
        for (int w = 0; w < StageFlags.VisitedRoomWords; w++)
            OrU32(baseAddr + GameMemoryAddresses.WorldFlags.OffVisitedRoom + (uint)(w * 4), bits.VisitedRoom[w]);
        if (bits.DungeonItem != 0)
        {
            uint addr = baseAddr + GameMemoryAddresses.WorldFlags.OffDungeonItem;
            var b = _dolphin.ReadMemory(addr, 1);
            if (b != null) _dolphin.WriteMemory(addr, new[] { (byte)(b[0] | bits.DungeonItem) });
        }
    }

    private void OrU32(uint addr, uint bits)
    {
        if (bits == 0) return;
        var b = _dolphin.ReadMemory(addr, 4);
        if (b == null) return;
        uint v = ReadU32(b, 0) | bits;
        _dolphin.WriteMemory(addr, new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v });
    }

    private static StageFlags Parse(byte[] buf, int off, int slot)
    {
        var f = new StageFlags
        {
            Slot = slot,
            Tbox = ReadU32(buf, off + GameMemoryAddresses.WorldFlags.OffTbox),
            Item = ReadU32(buf, off + GameMemoryAddresses.WorldFlags.OffItem),
            DungeonItem = buf[off + GameMemoryAddresses.WorldFlags.OffDungeonItem],
        };
        for (int w = 0; w < StageFlags.SwitchWords; w++)
            f.Switch[w] = ReadU32(buf, off + GameMemoryAddresses.WorldFlags.OffSwitch + w * 4);
        for (int w = 0; w < StageFlags.VisitedRoomWords; w++)
            f.VisitedRoom[w] = ReadU32(buf, off + GameMemoryAddresses.WorldFlags.OffVisitedRoom + w * 4);
        return f;
    }

    private static uint ReadU32(byte[] b, int o) =>
        (uint)(b[o] << 24 | b[o + 1] << 16 | b[o + 2] << 8 | b[o + 3]);

    private static StageFlags[] NewSlots() =>
        Enumerable.Range(0, StageFlags.SlotCount).Select(i => new StageFlags { Slot = i }).ToArray();

    public void Dispose() => Stop();
}

/// <summary>
/// Memory switches are applied edge-triggered, never enforced: the game legitimately turns some
/// OFF again (timed switches, pressure plates, torches) and re-applying would latch them on. A
/// switch bit from the room is written at most once per slot, and never if this game has ever had
/// it set itself (it set it and may have cleared it since — the room's copy would re-latch it).
/// Everything else in dSv_memBit_c is never cleared by the game, so it's enforced every tick.
/// </summary>
public sealed class SwitchApplyGuard
{
    private readonly uint[][] _had = Enumerable.Range(0, StageFlags.SlotCount)
        .Select(_ => new uint[StageFlags.SwitchWords]).ToArray();

    /// <summary>
    /// Record the switch bits <paramref name="local"/> (this game's current flags for
    /// <paramref name="slot"/>) has set, then clear from <paramref name="missing"/> every switch bit
    /// this slot has ever had; the switch bits left in it are about to be applied, so they're
    /// recorded too.
    /// </summary>
    public void Filter(int slot, StageFlags local, StageFlags missing)
    {
        var had = _had[slot];
        for (int w = 0; w < StageFlags.SwitchWords; w++)
        {
            had[w] |= local.Switch[w];
            missing.Switch[w] &= ~had[w];
            had[w] |= missing.Switch[w];
        }
    }
}
