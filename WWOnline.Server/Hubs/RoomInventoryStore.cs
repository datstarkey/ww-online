using WWOnline.Shared.Models;

namespace WWOnline.Server.Hubs;

/// <summary>
/// The room inventory for this server's lifetime. Players only ever add to it (gains merge);
/// the room owner can replace it outright. Every change bumps <see cref="RoomInventory.Revision"/> so
/// clients can drop out-of-order pushes. Thread-safe (hub methods run concurrently).
/// </summary>
public class RoomInventoryStore
{
    private readonly object _lock = new();
    private RoomInventory? _room;
    private long _revision;

    /// <param name="Room">A copy of the room state after the operation.</param>
    /// <param name="Changes">What changed (empty if nothing).</param>
    /// <param name="Seeded">True when this call created the room.</param>
    public readonly record struct Result(RoomInventory Room, List<string> Changes, bool Seeded);

    /// <summary>Merge <paramref name="gains"/> (a normalized, validated payload) into the room; the first one seeds it.</summary>
    public Result Merge(RoomInventory gains)
    {
        lock (_lock)
        {
            bool seeded = _room == null;
            var before = _room ?? new RoomInventory();
            var after = before.Clone();
            bool changed = after.MergeGainsFrom(gains);
            if (seeded || changed)
            {
                after.Revision = ++_revision;
                _room = after;
            }
            return new Result(_room!.Clone(), changed ? RoomInventory.Describe(before, after) : [], seeded);
        }
    }

    /// <summary>Room owner edit: the room becomes exactly <paramref name="exact"/> (may remove or downgrade).</summary>
    public Result Replace(RoomInventory exact)
    {
        lock (_lock)
        {
            bool seeded = _room == null;
            var before = _room ?? new RoomInventory();
            var changes = RoomInventory.Describe(before, exact);
            if (seeded || changes.Count > 0)
            {
                _room = exact.Clone();
                _room.Revision = ++_revision;
            }
            return new Result(_room!.Clone(), changes, seeded);
        }
    }

    /// <summary>True once someone has seeded the room (joined, or the owner set it).</summary>
    public bool IsSeeded
    {
        get { lock (_lock) return _room != null; }
    }

    /// <summary>The current room, or null if nobody has joined yet.</summary>
    public RoomInventory? Snapshot()
    {
        lock (_lock) return _room?.Clone();
    }
}
