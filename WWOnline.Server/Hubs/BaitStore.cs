using WWOnline.Shared.Models;

namespace WWOnline.Server.Hubs;

/// <summary>
/// The shared bait bag (Shared bait bag rule): one All-Purpose Bait count and one Hyoi Pear count (see
/// <see cref="BagCountsStore{T}"/>). Each type stays in range, and together they always fit the bag's 8
/// slots: a gain that would overfill it (two players buying the last slot at once) is cut down to what fits.
/// </summary>
public class BaitStore : BagCountsStore<BaitCounts>
{
    protected override BaitCounts AddDelta(BaitCounts total, BaitCounts delta) => Add(total, delta);

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
}
