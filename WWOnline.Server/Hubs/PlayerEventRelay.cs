using System.Collections.Concurrent;
using WWOnline.Shared.Models;

namespace WWOnline.Server.Hubs;

/// <summary>
/// Who gets a player's projectile events (<see cref="PlayerEvent"/>), and whether an event is let
/// through at all. The server knows where everyone is from their puppet data (stage, room,
/// position, 20 times a second; a location with no update for <see cref="LocationTtl"/> is forgotten,
/// so a player whose game is detached neither sends nor gets events). An event goes to the players
/// in the room where it happened (its own stage and room: a bomb thrown in room 1 explodes in room 1
/// even after its thrower walked on), or at sea to those within the sea hide distance of it, by the
/// same rule the clients use for puppets (<see cref="PuppetVisibility"/>). An event is dropped when
/// it is malformed, when its sender has no known location, when it claims another stage or a spot
/// far from the sender, when it uses another connected player's origin (receivers de-duplicate by
/// origin + seq, so a stolen origin with a huge seq would silence that player), or when the sender is
/// over its rate (a client can't spam explosions into other games).
/// Thread-safe: hub calls run concurrently.
/// </summary>
public sealed class PlayerEventRelay
{
    /// <summary>Burst of events a player may send at once: a few bombs and a cannon volley, each with
    /// its throw and its explosion, fit easily.</summary>
    public const double BurstEvents = 20;

    /// <summary>Sustained events per second (a bomb is 2-3 events, the cannon fires once a second).</summary>
    public const double EventsPerSecond = 10;

    /// <summary>How far (horizontally) from the sender's last known position an event may be: a
    /// cannonball flies 110 units a frame for a few seconds, a thrown bomb much less.</summary>
    public const float MaxDistanceFromSender = 20000f;

    /// <summary>A player's location is forgotten this long after their last puppet update (clients send
    /// 20 a second while their game is attached; puppets are dropped after 3 s too).</summary>
    public static readonly TimeSpan LocationTtl = TimeSpan.FromSeconds(3);

    public enum Verdict
    {
        Relay,
        Invalid,
        NoLocation,
        WrongPlace,
        RateLimited,
        /// <summary>The event's origin belongs to another connected player (or this connection already
        /// sends under another origin): it could be used to make receivers drop that player's events.</summary>
        ForeignOrigin,
    }

    private readonly record struct Location(string Stage, byte Room, Vector3 Position, DateTime At);

    private sealed class Bucket
    {
        public double Tokens = BurstEvents;
        public DateTime Last;
    }

    private readonly ConcurrentDictionary<string, Location> _locations = new();
    private readonly ConcurrentDictionary<string, Bucket> _buckets = new();

    // PlayerEvent.Origin -> the connection sending under it, and back. An origin is taken by the first
    // connection that uses it and freed when that connection leaves. It can also be taken over once its
    // owner's location is stale (no puppet data for LocationTtl): after an unclean drop the old connection
    // lingers until the server's client timeout (30 s), and the client's new connection must not be shut
    // out meanwhile. While the owner is live it is never given away. One origin per connection.
    private readonly Dictionary<string, string> _originOwner = new();
    private readonly Dictionary<string, string> _connectionOrigin = new();
    private readonly object _originLock = new();

    /// <summary>Remember where a player is (from each valid puppet update).</summary>
    public void UpdateLocation(string connectionId, PuppetData data, DateTime utcNow)
    {
        if (string.IsNullOrEmpty(connectionId) || data?.Position == null || !data.Position.IsFinite())
            return;
        _locations[connectionId] = new Location(data.StageName ?? "", data.RoomNumber,
            new Vector3(data.Position.X, data.Position.Y, data.Position.Z), utcNow);
    }

    private bool TryGetFresh(string connectionId, DateTime utcNow, out Location at) =>
        _locations.TryGetValue(connectionId, out at) && utcNow - at.At <= LocationTtl;

    /// <summary>The player left: forget their location and rate.</summary>
    public void Forget(string connectionId)
    {
        _locations.TryRemove(connectionId, out _);
        _buckets.TryRemove(connectionId, out _);
        lock (_originLock)
        {
            if (_connectionOrigin.Remove(connectionId, out var origin))
                _originOwner.Remove(origin);
        }
    }

    /// <summary>Claim (or confirm) <paramref name="origin"/> for <paramref name="connectionId"/>.</summary>
    private bool ClaimOrigin(string connectionId, string origin, DateTime utcNow)
    {
        lock (_originLock)
        {
            if (_originOwner.TryGetValue(origin, out var owner))
            {
                if (owner == connectionId)
                    return true;
                if (TryGetFresh(owner, utcNow, out _))
                    return false; // its owner is live
            }
            if (_connectionOrigin.ContainsKey(connectionId))
                return false; // one origin per connection
            if (owner != null)
                _connectionOrigin.Remove(owner); // the stale owner loses it
            _originOwner[origin] = connectionId;
            _connectionOrigin[connectionId] = origin;
            return true;
        }
    }

    /// <summary>
    /// May <paramref name="senderId"/>'s event be relayed? Spends one of the sender's rate tokens
    /// when it may (a rejected event costs nothing, so a bad one can't lock out good ones).
    /// </summary>
    public Verdict Check(string senderId, PlayerEvent? evt, DateTime utcNow)
    {
        if (evt == null || !evt.IsValid())
            return Verdict.Invalid;
        if (!TryGetFresh(senderId, utcNow, out var at))
            return Verdict.NoLocation;
        if (evt.StageName != at.Stage)
            return Verdict.WrongPlace;
        float dx = evt.Position.X - at.Position.X;
        float dz = evt.Position.Z - at.Position.Z;
        if (dx * dx + dz * dz > MaxDistanceFromSender * MaxDistanceFromSender)
            return Verdict.WrongPlace;
        if (!ClaimOrigin(senderId, evt.Origin, utcNow))
            return Verdict.ForeignOrigin;

        var bucket = _buckets.GetOrAdd(senderId, _ => new Bucket { Last = utcNow });
        lock (bucket)
        {
            double elapsed = Math.Max(0, (utcNow - bucket.Last).TotalSeconds);
            bucket.Last = utcNow;
            bucket.Tokens = Math.Min(BurstEvents, bucket.Tokens + elapsed * EventsPerSecond);
            if (bucket.Tokens < 1)
                return Verdict.RateLimited;
            bucket.Tokens -= 1;
        }
        return Verdict.Relay;
    }

    /// <summary>
    /// The other players (with a fresh location) where <paramref name="evt"/> happened: its stage and room,
    /// or at sea within the sea hide distance of its position. Never the sender.
    /// </summary>
    public List<string> Recipients(string senderId, PlayerEvent evt, DateTime utcNow)
    {
        var result = new List<string>();
        foreach (var (id, at) in _locations)
        {
            if (id == senderId || utcNow - at.At > LocationTtl)
                continue;
            if (PuppetVisibility.IsSameLocation(at.Stage, at.Room, at.Position.X, at.Position.Z,
                                                evt.StageName, evt.RoomNumber, evt.Position, wasVisible: true))
                result.Add(id);
        }
        return result;
    }
}
