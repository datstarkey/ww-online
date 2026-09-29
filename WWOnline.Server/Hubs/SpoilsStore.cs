using WWOnline.Shared.Models;

namespace WWOnline.Server.Hubs;

/// <summary>
/// The shared spoils bag (Shared spoils bag rule): one count per spoil type (see <see cref="BagCountsStore{T}"/>),
/// each clamped to 0-99 like the game's own (d_meter.cpp). Each type has at most one of the 8 bag slots, so
/// any mix fits.
/// </summary>
public class SpoilsStore : BagCountsStore<SpoilsCounts>
{
    protected override SpoilsCounts AddDelta(SpoilsCounts total, SpoilsCounts delta) => Add(total, delta);

    public static SpoilsCounts Add(SpoilsCounts total, SpoilsCounts delta)
    {
        var sum = new int[SpoilsCounts.TypeCount];
        for (int i = 0; i < SpoilsCounts.TypeCount; i++)
            sum[i] = Math.Clamp(total.Counts[i] + delta.Counts[i], 0, SpoilsCounts.MaxCount);
        return new SpoilsCounts(sum);
    }
}
