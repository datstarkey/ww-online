using System.Collections.Concurrent;
using WWOnline.Shared.Models;

namespace WWOnline.Server.Hubs;

/// <summary>
/// Whether a player's <see cref="PvpHit"/> goes to its target. A hit is relayed only while the room's Pvp rule is on,
/// between two named players whose games take PVP hits (<see cref="ClientCapabilities.PvpHits"/>: a Dolphin player is
/// never hit), when both have a fresh location (their puppet data, kept by <see cref="PlayerEventRelay"/>) in the hit's
/// stage, the hit claims an attacker near the sender and a spot near the target, and the sender is under its rate.
/// Thread-safe: hub calls run concurrently.
/// </summary>
public sealed class PvpHitGate(PlayerEventRelay locations)
{
    /// <summary>Hits a player may land at once (a sword combo, a spin attack on two players).</summary>
    public const double BurstHits = 10;

    /// <summary>Sustained hits per second.</summary>
    public const double HitsPerSecond = 6;

    /// <summary>How far (horizontally) the hit's attacker and its spot may be from the sender's and the target's last
    /// known positions (20 puppet updates a second; Link runs about 20 units a frame).</summary>
    public const float MaxDistance = 1000f;

    public enum Verdict
    {
        Relay,
        Invalid,
        RuleOff,
        /// <summary>The sender's game doesn't take PVP hits (or they never joined with a name).</summary>
        SenderCannotPvp,
        /// <summary>The target isn't a named player here, or is the sender.</summary>
        BadTarget,
        /// <summary>The target's game doesn't take PVP hits (Dolphin).</summary>
        TargetCannotPvp,
        NoLocation,
        WrongStage,
        TooFar,
        RateLimited,
    }

    private readonly ConcurrentDictionary<string, ClientCapabilities> _capabilities = new();
    private readonly RateBuckets _buckets = new(BurstHits, HitsPerSecond);

    /// <summary>A named player joined with these capabilities.</summary>
    public void Join(string connectionId, ClientCapabilities capabilities) => _capabilities[connectionId] = capabilities;

    /// <summary>The player left.</summary>
    public void Forget(string connectionId)
    {
        _capabilities.TryRemove(connectionId, out _);
        _buckets.Forget(connectionId);
    }

    private bool CanPvp(string id) =>
        _capabilities.TryGetValue(id, out var c) && c.HasFlag(ClientCapabilities.PvpHits);

    /// <summary>May <paramref name="senderId"/>'s hit go to its target? Spends one of the sender's rate tokens when it
    /// may (a rejected hit costs nothing).</summary>
    public Verdict Check(string senderId, PvpHit? hit, bool ruleOn, DateTime utcNow)
    {
        if (hit == null || !hit.IsValid())
            return Verdict.Invalid;
        if (!ruleOn)
            return Verdict.RuleOff;
        if (!CanPvp(senderId))
            return Verdict.SenderCannotPvp;
        if (hit.TargetId == senderId || !_capabilities.ContainsKey(hit.TargetId))
            return Verdict.BadTarget;
        if (!CanPvp(hit.TargetId))
            return Verdict.TargetCannotPvp;
        if (!locations.TryGetFresh(senderId, utcNow, out var sender) || !locations.TryGetFresh(hit.TargetId, utcNow, out var target))
            return Verdict.NoLocation;
        if (hit.StageName != sender.Stage || hit.StageName != target.Stage)
            return Verdict.WrongStage;
        if (!Near(hit.AttackerPosition, sender.Position) || !Near(hit.Position, target.Position))
            return Verdict.TooFar;
        return _buckets.TrySpend(senderId, utcNow) ? Verdict.Relay : Verdict.RateLimited;
    }

    private static bool Near(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X;
        float dz = a.Z - b.Z;
        return dx * dx + dz * dz <= MaxDistance * MaxDistance;
    }
}
