using Ambiquality.Ingestion.Api.Application.Abstractions;
using StackExchange.Redis;

namespace Ambiquality.Ingestion.Api.Infrastructure.RateLimiting;

/// <summary>
/// Fixed-window per-sensor rate limiter backed by Redis. Each hit is an atomic
/// <c>INCR</c>; the first hit of a window also sets the key's TTL to the window length,
/// so the counter resets when the window elapses. The counter lives in the same Redis
/// the ingestion queue uses — but on a separate keyspace (<see cref="RateLimitOptions.KeyPrefix"/>),
/// and unlike the stream it is throw-away state (no AOF durability is required: losing it
/// merely resets a sensor's window, which fails open, never rejecting a legitimate batch).
/// </summary>
public sealed class RedisFixedWindowRateLimiter(IConnectionMultiplexer redis) : IRateLimiter
{
    public async Task<RateLimitDecision> HitAsync(
        string key, int windowSeconds, int permitsPerWindow, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        RedisKey redisKey = key;

        // INCR returns the post-increment count; on the first hit it is 1 and we stamp the
        // window TTL. Setting the expiry only on the first hit gives a true fixed window —
        // later hits in the same window do not slide it forward.
        var count = await db.StringIncrementAsync(redisKey);
        if (count == 1)
        {
            await db.KeyExpireAsync(redisKey, TimeSpan.FromSeconds(windowSeconds));
            return RateLimitDecision.Allow;
        }

        if (count <= permitsPerWindow)
            return RateLimitDecision.Allow;

        // Over the allowance: report the seconds left in the window. A missing TTL means a
        // crash landed between INCR and EXPIRE — re-stamp it so the key cannot leak forever.
        var ttl = await db.KeyTimeToLiveAsync(redisKey);
        if (ttl is null)
        {
            await db.KeyExpireAsync(redisKey, TimeSpan.FromSeconds(windowSeconds));
            ttl = TimeSpan.FromSeconds(windowSeconds);
        }

        return RateLimitDecision.Deny((int)Math.Ceiling(ttl.Value.TotalSeconds));
    }
}
