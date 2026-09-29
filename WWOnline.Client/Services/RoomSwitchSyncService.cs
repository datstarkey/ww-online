using System.Collections.Concurrent;
using Serilog;
using WWOnline.Data;
using WWOnline.Hubs;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>
/// Live room switches (docs/live-world.md §5, stage 2): the dungeon-visit (dan, 0x80-0xBF) and room (zone,
/// 0xC0-0xEF) switches that ladders, torches, crystals, floor switches and kill-all rooms use are shared
/// between players in the same stage (dan) or the same room (zone), while the Shared world rule is on.
/// The objects poll their switch, so writing the bit into the live save data is enough: another player's
/// ladder drops (with its own camera event) in your game too.
///
/// Only switches the patch-time <see cref="Patcher.WorldData.SwitchTable"/> says are latching are read,
/// sent or applied (no timers, timed torches / crystals, pressure plates, area switches, push blocks), and
/// only on-edges: the server ORs them, nobody clears them (<see cref="RoomSwitchTracker"/>: echo guard,
/// apply once). Every tick (4Hz, scene stable, the same place for 1 s):
///   - a new place (stage, slot, room) joins the server's store for it, which returns what the players
///     already there set;
///   - newly set local bits are sent; room bits this game is missing (and never had here) are ORed in.
/// Applied bits also go to <see cref="LiveWorldPoke"/> for the actors that read them only at create.
/// </summary>
public sealed class RoomSwitchSyncService : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<RoomSwitchSyncService>();

    private const int TickMs = 250;
    private const int StableTicksRequired = 4;       // same stage with Link present (SceneStabilityGate)
    private const int PlaceTicksRequired = 4;        // same room for 1 s before we touch its switches
    private static readonly TimeSpan JoinRetry = TimeSpan.FromSeconds(3);

    private readonly IDolphinService _dolphin;
    private readonly SignalRClientService _signalR;
    private readonly RoomSettingsService _room;
    private readonly SwitchTableProvider _tables;
    private readonly LiveWorldPoke _liveWorld;
    private readonly SceneStabilityGate _scene = new(StableTicksRequired);
    private readonly ConcurrentQueue<RoomSwitches> _inbox = new();
    private readonly object _tickLock = new();
    private System.Timers.Timer? _timer;

    // Guarded by _tickLock
    private readonly RoomSwitchTracker _tracker = new();
    private RoomSwitches? _candidatePlace;
    private int _placeTicks;
    private DateTime _nextJoinAttempt;
    private int _generation;
    private bool _sending;
    private int _joining;

    public RoomSwitchSyncService(IDolphinService dolphin, SignalRClientService signalR, RoomSettingsService room,
        SwitchTableProvider tables, LiveWorldPoke liveWorld)
    {
        _dolphin = dolphin;
        _signalR = signalR;
        _room = room;
        _tables = tables;
        _liveWorld = liveWorld;
    }

    public void Start()
    {
        if (_timer != null) return;
        _signalR.RoomSwitchesReceived += OnReceived;
        _signalR.Connected += OnConnected;
        _room.Changed += OnRulesChanged;
        _timer = new System.Timers.Timer(TickMs) { AutoReset = true };
        _timer.Elapsed += (_, _) =>
        {
            if (!Monitor.TryEnter(_tickLock)) return; // skip rather than overlap a slow tick
            try { Tick(); }
            catch (Exception ex) { Logger.Error(ex, "[switches] tick failed"); }
            finally { Monitor.Exit(_tickLock); }
        };
        _timer.Start();
        Logger.Information("[switches] live room switch sync started");
    }

    public void Stop()
    {
        if (_timer == null) return;
        _signalR.RoomSwitchesReceived -= OnReceived;
        _signalR.Connected -= OnConnected;
        _room.Changed -= OnRulesChanged;
        _timer.Stop();
        _timer.Dispose();
        _timer = null;
        lock (_tickLock) Leave();
        Logger.Information("[switches] live room switch sync stopped");
    }

    private void OnReceived(RoomSwitches switches)
    {
        if (switches != null && switches.IsValid()) _inbox.Enqueue(switches.Clone());
    }

    // A new connection (the server may have restarted with an empty store), or the room rules changed (the
    // server clears the store when Shared world goes off, possibly on and off again between two ticks):
    // join the place again.
    private void OnConnected() => RejoinSoon();

    private void OnRulesChanged(RoomSettings _) => RejoinSoon();

    private void RejoinSoon()
    {
        lock (_tickLock)
        {
            _generation++;
            _tracker.Rejoin();
            _nextJoinAttempt = DateTime.MinValue;
            _inbox.Clear();
        }
    }

    private void Leave()
    {
        _generation++;
        _tracker.Reset();
        _candidatePlace = null;
        _placeTicks = 0;
        _inbox.Clear();
    }

    private void Tick()
    {
        if (!_dolphin.IsConnected) return;

        if (!_room.Current.SharedWorld)
        {
            if (_tracker.Place != null)
            {
                Leave();
                Logger.Information("[switches] shared world off — room switches not synced");
            }
            return;
        }
        if (!_scene.Check(_dolphin))
        {
            // Loading, title / file select, no Link (void-out, game over, soft reset): whatever comes next
            // reloads the room's switches, even at the same place, so we leave and join it afresh.
            if (_tracker.Place != null || _candidatePlace != null)
                Leave();
            return;
        }
        if (_tables.Table is not { } table) return;

        var here = ReadPlace();
        if (here == null) return;
        if (_candidatePlace == null || !_candidatePlace.SamePlace(here))
        {
            _candidatePlace = here;
            _placeTicks = 0;
        }
        if (++_placeTicks < PlaceTicksRequired) return; // just arrived: the room may still be loading

        var danMask = table.DanWords(here.Stage);
        var zoneMask = table.ZoneWords(here.Stage, here.Room);
        var local = ReadLocal(here, out bool ready);
        if (local == null) return;
        var mine = local.Masked(danMask, zoneMask);

        if (_tracker.MoveTo(here))
        {
            _generation++;
            _inbox.Clear();
            _nextJoinAttempt = DateTime.MinValue;
            Logger.Information("[switches] at {Stage} slot {Slot} room {Room}: {Dan} dan / {Zone} room switch(es) syncable here",
                here.Stage, here.Slot, here.Room,
                danMask.Sum(w => System.Numerics.BitOperations.PopCount(w)), zoneMask.Sum(w => System.Numerics.BitOperations.PopCount(w)));
        }
        _liveWorld.SetZoneRoom(here.Room); // every tick: a stage change on the world-sync timer resets it
        _liveWorld.SetLive(LiveWorldPoke.SwitchWord(0x80), [local.Dan[0], local.Dan[1], local.Zone[0], local.Zone[1]]);

        // mDan not (yet) this slot's, or the room has no zone yet: nothing to read or write here.
        if (!ready || !_signalR.IsConnected) return;
        if (!_tracker.Joined)
        {
            TryJoin(mine);
            return;
        }

        while (_inbox.TryDequeue(out var incoming))
        {
            var bits = incoming.Masked(danMask, zoneMask);
            if (!bits.IsEmpty && _tracker.OnReceived(bits))
                Logger.Information("[switches] room set {Bits}", bits);
        }

        // Inbound first (so this tick's local read doesn't count them as this game's own).
        var apply = _tracker.ToApply(mine);
        _tracker.Observe(mine);
        if (!apply.IsEmpty)
        {
            var written = Apply(apply);
            _tracker.MarkApplied(written); // only what landed: the rest is tried again next tick
            if (!written.IsEmpty)
            {
                _liveWorld.AddRemote(here.Slot, [0, 0, 0, 0, 0, written.Dan[0], written.Dan[1], written.Zone[0], written.Zone[1]]);
                Logger.Information("[switches] applied {Bits}", written);
            }
        }

        // Outbound: switches this game set that the room hasn't had from us or anyone.
        var fresh = _tracker.Fresh(mine);
        if (!fresh.IsEmpty && !_sending)
            Send(fresh);
    }

    private void Send(RoomSwitches fresh)
    {
        int generation = _generation;
        _sending = true;
        _tracker.MarkSent(fresh);
        Logger.Information("[switches] local set {Bits} → sending", fresh);
        _ = Task.Run(async () =>
        {
            bool? taken = null;
            try { taken = await _signalR.SendRoomSwitchesAsync(fresh); }
            catch (Exception ex) { Logger.Warning(ex, "[switches] send failed"); }
            lock (_tickLock)
            {
                _sending = false;
                if (generation != _generation) return;
                if (taken == false)
                {
                    // The server has no record of us here (its store was cleared): join again, which sends it all.
                    Logger.Information("[switches] the server lost our place — rejoining");
                    _generation++;
                    _tracker.Rejoin();
                    _nextJoinAttempt = DateTime.MinValue;
                }
                else if (taken != true)
                    _tracker.Unsent(fresh); // offline or failed: retried next tick
            }
        });
    }

    private void TryJoin(RoomSwitches mine)
    {
        if (DateTime.UtcNow < _nextJoinAttempt) return;
        if (Interlocked.Exchange(ref _joining, 1) == 1) return;
        _nextJoinAttempt = DateTime.UtcNow + JoinRetry;
        int generation = _generation;
        var snapshot = mine.Clone();
        _ = Task.Run(async () =>
        {
            try
            {
                var state = await _signalR.JoinRoomSwitchesAsync(snapshot);
                if (state == null || !state.IsValid()) return; // offline / rule off: retried
                lock (_tickLock)
                {
                    if (generation != _generation || !_tracker.OnJoined(snapshot, state)) return; // moved meanwhile
                }
                Logger.Information("[switches] joined {Stage} room {Room}: brought {Mine} switch(es), the room holds {Held}",
                    state.Stage, state.Room, snapshot.BitCount, state.BitCount);
            }
            catch (Exception ex) { Logger.Warning(ex, "[switches] join failed (retrying)"); }
            finally { Interlocked.Exchange(ref _joining, 0); }
        });
    }

    /// <summary>Stage name, save slot (dStage_stagInfo_GetSaveTbl) and stay room, or null if unreadable.</summary>
    private RoomSwitches? ReadPlace()
    {
        var name = _dolphin.ReadMemory(GameMemoryAddresses.Stage.CurrentStageName.Address, 8);
        var room = _dolphin.ReadMemory(GameMemoryAddresses.Stage.CurrentRoomNumber.Address, 1);
        var ptr = _dolphin.ReadMemory(GameMemoryAddresses.WorldFlags.StagInfoPtr, 4);
        if (name == null || room == null || ptr is not { Length: 4 }) return null;
        uint stag = ReadU32(ptr, 0);
        if (stag < 0x80000000 || stag >= 0x84000000) return null;
        var prop = _dolphin.ReadMemory(stag + GameMemoryAddresses.WorldFlags.StagSaveTblOffset, 1);
        if (prop == null) return null;

        int nul = Array.IndexOf(name, (byte)0);
        var place = new RoomSwitches
        {
            Stage = System.Text.Encoding.ASCII.GetString(name, 0, nul >= 0 ? nul : name.Length),
            Slot = (prop[0] >> 1) & 0x7F,
            Room = room[0],
        };
        return place.IsValid() ? place : null;
    }

    /// <summary>
    /// This game's dan switches and the stay room's zone switches, or null if unreadable.
    /// <paramref name="ready"/> is false while mDan isn't this slot's or the room has no zone (then the
    /// missing part reads as 0 and nothing may be applied: it would be lost).
    /// </summary>
    private RoomSwitches? ReadLocal(RoomSwitches place, out bool ready)
    {
        ready = false;
        var local = place.EmptyCopy();
        var dan = _dolphin.ReadMemory(GameMemoryAddresses.WorldFlags.LiveDan, GameMemoryAddresses.WorldFlags.DanOffSwitch + 8);
        if (dan == null) return null;
        bool danReady = (sbyte)dan[GameMemoryAddresses.WorldFlags.DanOffStageNo] == place.Slot;
        if (danReady)
        {
            local.Dan[0] = ReadU32(dan, GameMemoryAddresses.WorldFlags.DanOffSwitch);
            local.Dan[1] = ReadU32(dan, GameMemoryAddresses.WorldFlags.DanOffSwitch + 4);
        }
        if (ZoneOf(place.Room) is int zone)
        {
            ready = danReady;
            var z = _dolphin.ReadMemory(GameMemoryAddresses.WorldFlags.Zone(zone) + GameMemoryAddresses.WorldFlags.ZoneOffSwitch, 6);
            if (z == null) return null;
            local.Zone[0] = (uint)(z[0] << 8 | z[1]) | (uint)(z[2] << 8 | z[3]) << 16;
            local.Zone[1] = (uint)(z[4] << 8 | z[5]);
        }
        return local;
    }

    /// <summary>The zone of <paramref name="room"/> (dStage_roomControl_c::getZoneNo), if it has a live one for that room.</summary>
    private int? ZoneOf(int room)
    {
        var zb = _dolphin.ReadMemory(GameMemoryAddresses.WorldFlags.RoomZoneNo(room), 1);
        if (zb == null) return null;
        int zone = (sbyte)zb[0];
        if (zone is < 0 or >= GameMemoryAddresses.WorldFlags.ZoneCount) return null;
        var owner = _dolphin.ReadMemory(GameMemoryAddresses.WorldFlags.Zone(zone) + GameMemoryAddresses.WorldFlags.ZoneOffRoomNo, 1);
        return owner != null && (sbyte)owner[0] == room ? zone : null;
    }

    /// <summary>
    /// OR room bits into the live save data: dan words (if mDan is this slot's), zone u16s (if the room has its
    /// zone). Returns the bits written.
    /// </summary>
    private RoomSwitches Apply(RoomSwitches bits)
    {
        var place = _tracker.Place!;
        var written = place.EmptyCopy();
        var dan = _dolphin.ReadMemory(GameMemoryAddresses.WorldFlags.LiveDan + GameMemoryAddresses.WorldFlags.DanOffStageNo, 1);
        if (dan != null && (sbyte)dan[0] == place.Slot)
        {
            for (int i = 0; i < RoomSwitches.DanWords; i++)
            {
                if (OrBits(GameMemoryAddresses.WorldFlags.LiveDan + (uint)(GameMemoryAddresses.WorldFlags.DanOffSwitch + i * 4), bits.Dan[i], 4))
                    written.Dan[i] = bits.Dan[i];
            }
        }
        if (ZoneOf(place.Room) is int zone)
        {
            uint sw = GameMemoryAddresses.WorldFlags.Zone(zone) + GameMemoryAddresses.WorldFlags.ZoneOffSwitch;
            if (OrBits(sw, bits.Zone[0] & 0xFFFF, 2)) written.Zone[0] |= bits.Zone[0] & 0xFFFF;
            if (OrBits(sw + 2, bits.Zone[0] >> 16, 2)) written.Zone[0] |= bits.Zone[0] & 0xFFFF0000;
            if (OrBits(sw + 4, bits.Zone[1] & 0xFFFF, 2)) written.Zone[1] |= bits.Zone[1] & 0xFFFF;
        }
        return written;
    }

    /// <summary>
    /// OR <paramref name="bits"/> into the big-endian <paramref name="size"/>-byte value at <paramref name="addr"/>,
    /// writing only the bytes that change (a single-byte write can't undo the game's change to a neighbour).
    /// True when the bits are set afterwards.
    /// </summary>
    private bool OrBits(uint addr, uint bits, int size)
    {
        if (bits == 0) return true;
        var b = _dolphin.ReadMemory(addr, size);
        if (b is not { Length: > 0 } || b.Length != size) return false;
        for (int i = 0; i < size; i++)
        {
            byte add = (byte)(bits >> (8 * (size - 1 - i)));
            if ((b[i] & add) == add) continue;
            if (!_dolphin.WriteMemory(addr + (uint)i, [(byte)(b[i] | add)])) return false;
        }
        return true;
    }

    private static uint ReadU32(byte[] b, int o) => (uint)(b[o] << 24 | b[o + 1] << 16 | b[o + 2] << 8 | b[o + 3]);

    public void Dispose() => Stop();
}
