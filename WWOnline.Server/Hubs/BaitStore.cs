using WWOnline.Shared.Models;

namespace WWOnline.Server.Hubs;

/// <summary>
/// The shared bait bag (Shared bait bag rule): one All-Purpose Bait count and one Hyoi Pear count, seeded
/// by the first allowed joiner's bag (see GameHub.JoinBait), then changed only by per-type deltas, like
/// the wallet. Unseeded, deltas are rejected: a stale client must not reseed it with just its delta.
/// Each type stays in range, and together they always fit the bag's 8 slots: a gain that would overfill
/// it (two players buying the last slot at once) is cut down to what fits. Thread-safe.
/// </summary>
public class BaitStore
{
    private readonly object _lock = new();
    private BaitCounts? _total;

    public bool IsSeeded
    {
        get { lock (_lock) return _total != null; }
    }

    /// <summary>
    /// Join with <paramref name="current"/> (a valid total, <see cref="BaitCounts.IsValidTotal"/>): seeds the
    /// bag if it's empty. Returns the counts to adopt.
    /// </summary>
    public (BaitCounts Total, bool Seeded) Join(BaitCounts current)
    {
        lock (_lock)
        {
            bool seeded = _total == null;
            _total ??= current.Clone();
            return (_total.Clone(), seeded);
        }
    }

    /// <summary>Apply a delta; returns the new total, or null if the delta is invalid or the bag isn't seeded.</summary>
    public BaitCounts? ApplyDelta(BaitCounts delta)
    {
        if (!delta.IsValidDelta()) return null;
        lock (_lock)
        {
            if (_total is not { } total) return null;
            _total = Add(total, delta);
            return _total.Clone();
        }
    }

    /// <summary>
    /// <paramref name="total"/> + <paramref name="delta"/>, each type clamped to its range; then, if the two
    /// no longer fit the 8 slots, the gains are trimmed (pears first, then bait) back towards
    /// <paramref name="total"/> until they do. Uses are never trimmed.
    /// </summary>
    public static BaitCounts Add(BaitCounts total, BaitCounts delta)
    {
        int bait = Math.Clamp(total.Bait + delta.Bait, 0, BaitCounts.MaxBait);
        int pears = Math.Clamp(total.Pears + delta.Pears, 0, BaitCounts.MaxPears);
        while (BaitCounts.SlotsNeeded(bait, pears) > BaitCounts.SlotCount && pears > total.Pears && delta.Pears > 0)
            pears--;
        while (BaitCounts.SlotsNeeded(bait, pears) > BaitCounts.SlotCount && bait > total.Bait && delta.Bait > 0)
            bait--;
        return new BaitCounts(bait, pears);
    }

    /// <summary>Forget the bag (rule turned off): the next join re-seeds it.</summary>
    public void Reset()
    {
        lock (_lock) _total = null;
    }
}
