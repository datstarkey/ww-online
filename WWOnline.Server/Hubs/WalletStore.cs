using WWOnline.Shared.Hubs;

namespace WWOnline.Server.Hubs;

/// <summary>
/// The shared rupee wallet: one total, seeded by the first allowed joiner's rupees (see
/// GameHub.JoinWallet), then changed only by deltas. Unseeded, deltas are rejected — a stale
/// client must not reseed it with just its delta. Thread-safe.
/// </summary>
public class WalletStore
{
    private readonly object _lock = new();
    private int? _total;

    /// <summary>A delta a client may send: non-zero and no bigger than a full wallet either way.</summary>
    public static bool IsValidDelta(int delta) =>
        delta != 0 && delta >= -HubConstants.MaxRupees && delta <= HubConstants.MaxRupees;

    public bool IsSeeded
    {
        get { lock (_lock) return _total != null; }
    }

    /// <summary>Join with <paramref name="currentRupees"/>: seeds the wallet if it's empty. Returns the total to adopt.</summary>
    public (int Total, bool Seeded) Join(int currentRupees)
    {
        lock (_lock)
        {
            bool seeded = _total == null;
            _total ??= Math.Clamp(currentRupees, 0, HubConstants.MaxRupees);
            return (_total.Value, seeded);
        }
    }

    /// <summary>Apply a delta; returns the new total, or null if the delta is invalid or the wallet isn't seeded.</summary>
    public int? ApplyDelta(int delta)
    {
        if (!IsValidDelta(delta)) return null;
        lock (_lock)
        {
            if (_total is not int total) return null;
            _total = Math.Clamp(total + delta, 0, HubConstants.MaxRupees);
            return _total;
        }
    }

    /// <summary>Forget the total (rule turned off): the next join re-seeds it.</summary>
    public void Reset()
    {
        lock (_lock) _total = null;
    }
}
