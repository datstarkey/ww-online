using WWOnline.Shared.Models;

namespace WWOnline.Server.Hubs;

/// <summary>
/// The shared world's live puzzle switches: dungeon-visit (dan) switches per stage (and save slot) and
/// room (zone) switches per stage room, the OR of what the players there set. Each connection is in one
/// place (stage, slot, room), set by <see cref="Join"/>. The game keeps dan switches per save slot
/// (dComIfGs_initDan resets them when the slot changes, d_save.cpp:1225-1234), but stages that share a
/// slot are separate visits (the sea caves: you always leave through the sea's slot), so they are kept
/// per stage. A stage's dan switches are dropped when its last player leaves, and a room's zone switches
/// when the last player leaves that room, so a fresh visit starts clean. Thread-safe.
/// </summary>
public class RoomSwitchStore
{
    private readonly object _lock = new();
    private readonly Dictionary<string, RoomSwitches> _where = new();     // connection → its place (bits unused)
    private readonly Dictionary<(int Slot, string Stage), uint[]> _dan = new();
    private readonly Dictionary<(string Stage, int Room), uint[]> _zone = new();

    /// <param name="State">Everything the store holds for the caller's place (what it should apply).</param>
    /// <param name="Added">The bits this call added (what the others should get).</param>
    /// <param name="SameRoom">Other connections in the same stage room (they get dan and zone bits).</param>
    /// <param name="SameStage">Other connections in the same stage but another room (dan bits only).</param>
    public readonly record struct Result(RoomSwitches State, RoomSwitches Added, IReadOnlyList<string> SameRoom,
        IReadOnlyList<string> SameStage);

    /// <summary>
    /// <paramref name="connectionId"/> is now at <paramref name="local"/>'s place (validated) with these bits
    /// set: move it there (dropping what nobody holds any more) and merge them in.
    /// </summary>
    public Result Join(string connectionId, RoomSwitches local)
    {
        lock (_lock)
        {
            if (_where.TryGetValue(connectionId, out var old) && !old.SamePlace(local))
            {
                _where.Remove(connectionId);
                DropUnoccupied(old);
            }
            _where[connectionId] = local.EmptyCopy();
            return MergeLocked(connectionId, local);
        }
    }

    /// <summary>
    /// Bits <paramref name="connectionId"/> set at its place. Null when it hasn't joined that place (it
    /// moved and must <see cref="Join"/> again): bits from anywhere else must not reach the store.
    /// </summary>
    public Result? Add(string connectionId, RoomSwitches gains)
    {
        lock (_lock)
        {
            if (!_where.TryGetValue(connectionId, out var at) || !at.SamePlace(gains)) return null;
            return MergeLocked(connectionId, gains);
        }
    }

    /// <summary>The connection left (disconnect): drop what nobody holds any more.</summary>
    public void Leave(string connectionId)
    {
        lock (_lock)
        {
            if (_where.Remove(connectionId, out var old))
                DropUnoccupied(old);
        }
    }

    /// <summary>The shared-world rule was turned off: forget everything (clients rejoin when it is back on).</summary>
    public void Clear()
    {
        lock (_lock)
        {
            _where.Clear();
            _dan.Clear();
            _zone.Clear();
        }
    }

    /// <summary>Dan words held for a stage (a copy; zeros if none).</summary>
    public uint[] DanOf(int slot, string stage)
    {
        lock (_lock) return (uint[])(_dan.GetValueOrDefault((slot, stage)) ?? new uint[RoomSwitches.DanWords]).Clone();
    }

    /// <summary>Zone words held for a stage room (a copy; zeros if none).</summary>
    public uint[] ZoneOf(string stage, int room)
    {
        lock (_lock) return (uint[])(_zone.GetValueOrDefault((stage, room)) ?? new uint[RoomSwitches.ZoneWords]).Clone();
    }

    private Result MergeLocked(string connectionId, RoomSwitches bits)
    {
        if (!_dan.TryGetValue((bits.Slot, bits.Stage), out var dan)) _dan[(bits.Slot, bits.Stage)] = dan = new uint[RoomSwitches.DanWords];
        if (!_zone.TryGetValue((bits.Stage, bits.Room), out var zone)) _zone[(bits.Stage, bits.Room)] = zone = new uint[RoomSwitches.ZoneWords];

        var added = bits.EmptyCopy();
        for (int i = 0; i < RoomSwitches.DanWords; i++) { added.Dan[i] = bits.Dan[i] & ~dan[i]; dan[i] |= bits.Dan[i]; }
        for (int i = 0; i < RoomSwitches.ZoneWords; i++) { added.Zone[i] = bits.Zone[i] & ~zone[i]; zone[i] |= bits.Zone[i]; }

        var state = bits.EmptyCopy();
        Array.Copy(dan, state.Dan, RoomSwitches.DanWords);
        Array.Copy(zone, state.Zone, RoomSwitches.ZoneWords);

        var sameRoom = new List<string>();
        var sameStage = new List<string>();
        foreach (var (id, at) in _where)
        {
            if (id == connectionId || at.Slot != bits.Slot || at.Stage != bits.Stage) continue;
            (at.SamePlace(bits) ? sameRoom : sameStage).Add(id);
        }
        return new Result(state, added, sameRoom, sameStage);
    }

    private void DropUnoccupied(RoomSwitches left)
    {
        if (!_where.Values.Any(w => w.Slot == left.Slot && w.Stage == left.Stage))
            _dan.Remove((left.Slot, left.Stage));
        if (!_where.Values.Any(w => w.Stage == left.Stage && w.Room == left.Room))
            _zone.Remove((left.Stage, left.Room));
    }
}
