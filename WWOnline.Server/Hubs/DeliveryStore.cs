using WWOnline.Shared.Models;

namespace WWOnline.Server.Hubs;

/// <summary>
/// The shared delivery bag (Shared delivery bag rule): how many of each quest item the room's bag holds, plus the
/// room's "ever obtained" bits (see <see cref="BagCountsStore{T}"/> and <see cref="DeliveryCounts"/>).
///   - Obtained bits are OR-merged: they only grow, and a delta's bits are kept even if its counts are refused.
///   - A delta that would take any item below zero is refused as a whole (its gains too). That is a trade or
///     hand-over of an item the room no longer has: two players trading the same Town Flower at once both send
///     "-1 Town Flower, +1 Sea Flower", and only the first counts, so the room ends with one Sea Flower, not two.
///     The refused player's game gets the room's bag back (the server pushes the total after every delta).
///   - A one-of-a-kind item (a letter, the Cabana Deed, a Beedle ticket: <see cref="DeliveryCounts.IsUnique"/>)
///     is never held twice: two players given the same letter before its obtained bit reached the other both
///     send +1, and the room keeps one.
///   - A gain that would overfill the 8 slots (two players receiving the last free slot's item at once) is cut
///     down to what fits, later items first.
///   - Windfall's pedestals (<see cref="DeliveryCounts.Pedestals"/>) change only from what the delta says they held
///     before: a delta that sets down or takes back an item on a pedestal the room has changed since is refused as
///     a whole, bag change included, so two players using the same pedestal at once never lose or copy an item.
/// </summary>
public class DeliveryStore : BagCountsStore<DeliveryCounts>
{
    protected override DeliveryCounts AddDelta(DeliveryCounts total, DeliveryCounts delta) => Add(total, delta);

    protected override string? Refusal(DeliveryCounts total, DeliveryCounts delta)
    {
        var stale = StalePedestals(total, delta).ToList();
        if (stale.Count > 0)
            return $"refused: pedestal {string.Join(", ", stale.Select(DeliveryCounts.PedestalName))} changed in the room first";
        var missing = Missing(total, delta).ToList();
        if (missing.Count > 0)
            return $"refused: the room's bag has no {Names(missing)} to give away";
        var added = Add(total, delta);
        var cut = Enumerable.Range(0, DeliveryCounts.TypeCount).Where(t => added[t] != total[t] + delta[t]).ToList();
        if (cut.Count == 0) return null;
        return cut.All(t => DeliveryCounts.IsUnique(t) && added[t] == 1)
            ? $"cut: the room already has {Names(cut)}"
            : $"cut: the bag's 8 slots can't take {Names(cut)}";
    }

    private static string Names(IEnumerable<int> types) => string.Join(", ", types.Select(t => DeliveryCounts.Types[t].Name));

    /// <summary>Types the delta takes out that the total doesn't have (enough of).</summary>
    private static IEnumerable<int> Missing(DeliveryCounts total, DeliveryCounts delta) =>
        Enumerable.Range(0, DeliveryCounts.TypeCount).Where(t => total[t] + delta[t] < 0);

    /// <summary>Pedestals the delta changes that no longer hold what it says they held.</summary>
    private static IEnumerable<int> StalePedestals(DeliveryCounts total, DeliveryCounts delta) =>
        delta.ChangedPedestals().Where(i => total.Pedestals[i] != delta.PedestalsBefore![i]);

    public static DeliveryCounts Add(DeliveryCounts total, DeliveryCounts delta)
    {
        uint obtained = (total.Obtained | delta.Obtained) & DeliveryCounts.KnownObtainedMask;
        if (Missing(total, delta).Any() || StalePedestals(total, delta).Any())
            return new DeliveryCounts(total.Counts, obtained, total.Pedestals);

        var pedestals = (byte[])total.Pedestals.Clone();
        foreach (int i in delta.ChangedPedestals()) pedestals[i] = delta.Pedestals[i];

        var sum = new int[DeliveryCounts.TypeCount];
        for (int t = 0; t < DeliveryCounts.TypeCount; t++)
        {
            sum[t] = total[t] + delta[t];
            // One of a kind: a second copy is a race (or a modded game), not a second item.
            if (DeliveryCounts.IsUnique(t) && delta[t] > 0) sum[t] = Math.Min(sum[t], Math.Max(total[t], 1));
        }

        // Overfilled: trim the gains, from the highest item number down, until it fits. Losses are never trimmed.
        for (int t = DeliveryCounts.TypeCount - 1; t >= 0 && sum.Sum() > DeliveryCounts.SlotCount; t--)
        {
            while (sum.Sum() > DeliveryCounts.SlotCount && sum[t] > total[t] && sum[t] > 0)
                sum[t]--;
        }
        return new DeliveryCounts(sum, obtained, pedestals);
    }
}
