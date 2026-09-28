namespace WWOnline.Server.Hubs;

/// <summary>
/// Who may seed an empty room store (items, story, wallet). Normally the claimed room owner's
/// game seeds it and everyone else waits. But if the owner never attaches a game (stays in menus,
/// no Dolphin), the room would never start — so once a non-owner has been waiting for
/// <see cref="Timeout"/>, the next joiner seeds it instead. With no claimed owner (dedicated
/// server, or the owner left) the first joiner seeds immediately. Thread-safe.
/// </summary>
public sealed class OwnerSeedGate
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    public enum Decision
    {
        /// <summary>Go ahead: the store is seeded, there's no claimed owner, or the caller is the owner.</summary>
        Proceed,
        /// <summary>Wait for the owner to seed (the client retries).</summary>
        Wait,
        /// <summary>The owner hasn't seeded within the timeout: the caller may seed.</summary>
        OwnerTimedOut,
    }

    private readonly object _lock = new();
    private readonly Func<DateTime> _clock;
    private readonly HashSet<string> _waiting = new();
    private DateTime? _firstWaitAt;

    public TimeSpan Timeout { get; }

    public OwnerSeedGate() : this(DefaultTimeout, () => DateTime.UtcNow) { }

    public OwnerSeedGate(TimeSpan timeout, Func<DateTime> clock)
    {
        Timeout = timeout;
        _clock = clock;
    }

    /// <param name="isSeeded">Whether the store already has a room.</param>
    /// <param name="ownerId">The claimed owner's connection id if they're connected, else null.</param>
    /// <param name="callerId">The joining connection.</param>
    /// <param name="firstWait">True the first time this caller is told to wait (log it once).</param>
    public Decision Check(bool isSeeded, string? ownerId, string callerId, out bool firstWait)
    {
        firstWait = false;
        if (isSeeded || ownerId == null || ownerId == callerId) return Decision.Proceed;
        lock (_lock)
        {
            var now = _clock();
            _firstWaitAt ??= now;
            if (now - _firstWaitAt.Value >= Timeout) return Decision.OwnerTimedOut;
            firstWait = _waiting.Add(callerId);
            return Decision.Wait;
        }
    }

    /// <summary>The store was seeded or reset: forget the waiters and the timeout clock.</summary>
    public void Reset()
    {
        lock (_lock)
        {
            _firstWaitAt = null;
            _waiting.Clear();
        }
    }

    /// <summary>A connection left.</summary>
    public void Forget(string connectionId)
    {
        lock (_lock) _waiting.Remove(connectionId);
    }
}
