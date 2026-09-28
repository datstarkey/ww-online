using WWOnline.Shared.Models;

namespace WWOnline.Server.Hubs;

/// <summary>
/// The shared world: the OR of every player's per-stage flags for this server's lifetime.
/// Late joiners get the whole thing on connect, so a chest opened before you joined is still
/// open for you. Thread-safe (hub methods run concurrently).
/// </summary>
public class WorldFlagStore
{
    private readonly object _lock = new();
    private readonly StageFlags[] _slots = Enumerable.Range(0, StageFlags.SlotCount)
        .Select(i => new StageFlags { Slot = i }).ToArray();

    /// <summary>
    /// Merge <paramref name="incoming"/>. Returns the slot's merged state if it gained any bits
    /// (so the caller can broadcast it), or null if nothing new.
    /// </summary>
    public (StageFlags Merged, int NewBits)? Merge(StageFlags incoming)
    {
        lock (_lock)
        {
            var slot = _slots[incoming.Slot];
            int before = slot.BitCount;
            if (!slot.MergeFrom(incoming)) return null;
            return (slot.Clone(), slot.BitCount - before);
        }
    }

    public List<StageFlags> Snapshot()
    {
        lock (_lock)
            return _slots.Where(s => !s.IsEmpty).Select(s => s.Clone()).ToList();
    }
}
