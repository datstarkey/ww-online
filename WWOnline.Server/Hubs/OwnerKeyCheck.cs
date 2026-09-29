using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace WWOnline.Server.Hubs;

/// <summary>
/// Checks a ClaimRoomOwner token against the server's owner key (the Host button's random token,
/// or a dedicated server's WWO_OWNER_KEY). The comparison is constant-time (SHA-256 of both, then
/// <see cref="CryptographicOperations.FixedTimeEquals"/>, so neither content nor length leaks),
/// tokens longer than <see cref="MaxKeyLength"/> are refused unhashed, and a connection that fails
/// <see cref="MaxFailedAttempts"/> times is ignored from then on (even with the right key), so a
/// weak key can't be guessed quickly on one connection and a client can't flood the log.
/// Thread-safe.
/// </summary>
public sealed class OwnerKeyCheck
{
    public const int MaxKeyLength = 256;
    public const int MaxFailedAttempts = 5;

    public enum Outcome
    {
        /// <summary>The right key: the caller owns the room.</summary>
        Accepted,
        /// <summary>Wrong key, no key configured, or a malformed token: log it.</summary>
        Rejected,
        /// <summary>Rejected, and that was this connection's last allowed attempt: log that it's now ignored.</summary>
        RejectedLastAttempt,
        /// <summary>This connection used up its attempts: do nothing, log nothing.</summary>
        Ignored,
    }

    private volatile byte[]? _keyHash;
    private readonly ConcurrentDictionary<string, int> _failures = new();

    public bool IsConfigured => _keyHash != null;

    /// <summary>Set (or clear, with null/empty) the owner key. Forgets every connection's failures.</summary>
    public void Configure(string? key)
    {
        _keyHash = string.IsNullOrEmpty(key) ? null : Hash(key);
        _failures.Clear();
    }

    public Outcome Check(string connectionId, string? token)
    {
        if (_failures.TryGetValue(connectionId, out var failed) && failed >= MaxFailedAttempts)
            return Outcome.Ignored;

        var key = _keyHash;
        if (key != null && token is { Length: > 0 and <= MaxKeyLength } &&
            CryptographicOperations.FixedTimeEquals(Hash(token), key))
        {
            _failures.TryRemove(connectionId, out _);
            return Outcome.Accepted;
        }

        var count = _failures.AddOrUpdate(connectionId, 1, (_, n) => n + 1);
        return count >= MaxFailedAttempts ? Outcome.RejectedLastAttempt : Outcome.Rejected;
    }

    /// <summary>A connection left.</summary>
    public void Forget(string connectionId) => _failures.TryRemove(connectionId, out _);

    private static byte[] Hash(string s) => SHA256.HashData(Encoding.UTF8.GetBytes(s));
}
