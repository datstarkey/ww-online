using System.Collections.Concurrent;

namespace WWOnline.Server.Hubs;

/// <summary>A token bucket per connection: <c>burst</c> at once, <c>perSecond</c> sustained. Thread-safe.</summary>
public sealed class RateBuckets(double burst, double perSecond)
{
    private sealed class Bucket
    {
        public double Tokens;
        public DateTime Last;
    }

    private readonly ConcurrentDictionary<string, Bucket> _buckets = new();

    /// <summary>Spend one of <paramref name="id"/>'s tokens; false when it has none left.</summary>
    public bool TrySpend(string id, DateTime utcNow)
    {
        var bucket = _buckets.GetOrAdd(id, _ => new Bucket { Tokens = burst, Last = utcNow });
        lock (bucket)
        {
            double elapsed = Math.Max(0, (utcNow - bucket.Last).TotalSeconds);
            bucket.Last = utcNow;
            bucket.Tokens = Math.Min(burst, bucket.Tokens + elapsed * perSecond);
            if (bucket.Tokens < 1)
                return false;
            bucket.Tokens -= 1;
            return true;
        }
    }

    public void Forget(string id) => _buckets.TryRemove(id, out _);
}
