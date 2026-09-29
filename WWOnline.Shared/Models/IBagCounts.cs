namespace WWOnline.Shared.Models;

/// <summary>
/// A bag's counts per item type, shared as one room total (<see cref="BaitCounts"/>, <see cref="SpoilsCounts"/>):
/// seeded by the room owner's bag, then changed only by signed per-type deltas. The server's store and the
/// client's sync service are written once against this.
/// </summary>
public interface IBagCounts<T> : IEquatable<T> where T : class, IBagCounts<T>
{
    /// <summary>A room total (or a game's bag): every type in range, and it fits the bag.</summary>
    bool IsValidTotal();

    /// <summary>A change a client may send: something changed, and nothing by more than a whole bag.</summary>
    bool IsValidDelta();

    T Clone();

    /// <summary>this - <paramref name="other"/>, per type.</summary>
    T Minus(T other);

    /// <summary>A signed change for the log, e.g. "-1 bait, +1 pear".</summary>
    string DeltaText();
}
