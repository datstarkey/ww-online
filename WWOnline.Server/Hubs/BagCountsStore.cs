using WWOnline.Shared.Models;

namespace WWOnline.Server.Hubs;

/// <summary>
/// A shared bag (bait, spoils): one room total, seeded by the first allowed joiner's bag (see GameHub's
/// JoinBait / JoinSpoils), then changed only by per-type deltas, like the wallet. Unseeded, deltas are
/// rejected: a stale client must not reseed it with just its delta. <see cref="Add"/> keeps the total
/// valid. Thread-safe.
/// </summary>
public abstract class BagCountsStore<T> where T : class, IBagCounts<T>
{
    private readonly object _lock = new();
    private T? _total;

    public bool IsSeeded
    {
        get { lock (_lock) return _total != null; }
    }

    /// <summary>total + delta, kept a valid total (each store's own clamping rules).</summary>
    protected abstract T AddDelta(T total, T delta);

    /// <summary>
    /// Join with <paramref name="current"/> (a valid total, <see cref="IBagCounts{T}.IsValidTotal"/>): seeds the
    /// bag if it's empty. Returns the counts to adopt.
    /// </summary>
    public (T Total, bool Seeded) Join(T current)
    {
        lock (_lock)
        {
            bool seeded = _total == null;
            _total ??= current.Clone();
            return (_total.Clone(), seeded);
        }
    }

    /// <summary>Apply a delta; returns the new total, or null if the delta is invalid or the bag isn't seeded.</summary>
    public T? ApplyDelta(T delta)
    {
        if (!delta.IsValidDelta()) return null;
        lock (_lock)
        {
            if (_total is not { } total) return null;
            _total = AddDelta(total, delta);
            return _total.Clone();
        }
    }

    /// <summary>Forget the bag (rule turned off): the next join re-seeds it.</summary>
    public void Reset()
    {
        lock (_lock) _total = null;
    }
}
