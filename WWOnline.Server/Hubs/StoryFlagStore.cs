using WWOnline.Shared.Models;

namespace WWOnline.Server.Hubs;

/// <summary>
/// The room's shared story flags for this server's lifetime: the OR of every player's syncable
/// event bits (<see cref="StoryFlags.SyncMask"/>) and Nintendo Gallery figurines
/// (<see cref="StoryFlags.Figurines"/>). The room owner's game seeds it (see
/// GameHub.JoinRoomStory); after that anyone's flags only ever merge up. Thread-safe (hub methods
/// run concurrently).
/// </summary>
public class StoryFlagStore
{
    private readonly object _lock = new();
    private StoryFlags? _room;

    /// <param name="Room">A copy of the room's flags after the merge.</param>
    /// <param name="Added">The bits this merge added (empty if nothing new).</param>
    /// <param name="Seeded">True when this call created the room.</param>
    public readonly record struct Result(StoryFlags Room, StoryFlags Added, bool Seeded);

    /// <summary>Merge <paramref name="incoming"/> (validated + normalized) into the room; the first call seeds it.</summary>
    public Result Merge(StoryFlags incoming)
    {
        lock (_lock)
        {
            bool seeded = _room == null;
            _room ??= new StoryFlags();
            var added = incoming.Clone().Normalize().Except(_room);
            _room.MergeFrom(added);
            return new Result(_room.Clone(), added, seeded);
        }
    }

    /// <summary>True once someone has seeded the room.</summary>
    public bool IsSeeded
    {
        get { lock (_lock) return _room != null; }
    }
}
