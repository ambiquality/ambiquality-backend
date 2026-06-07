using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace Ambiquality.Public.Api.Infrastructure.Caching;

/// <summary>
/// Read-through JSON caching over <see cref="IDistributedCache"/> that <em>degrades
/// gracefully</em>: any cache fault (e.g. Redis unreachable) is swallowed and the value is
/// computed fresh, so a cache outage never breaks the read path (Availability NFR). The
/// cache is backed by Redis in production and by an in-memory store when no Redis
/// connection is configured (tests, local single-process runs).
/// </summary>
public static class JsonDistributedCache
{
    public static async Task<T> GetOrCreateAsync<T>(
        IDistributedCache cache,
        string key,
        TimeSpan ttl,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken ct)
    {
        try
        {
            var cached = await cache.GetStringAsync(key, ct);
            if (cached is not null)
            {
                var hit = JsonSerializer.Deserialize<T>(cached);
                if (hit is not null)
                    return hit;
            }
        }
        catch
        {
            // Cache read failed — fall through and compute fresh.
        }

        var value = await factory(ct);

        try
        {
            var payload = JsonSerializer.Serialize(value);
            await cache.SetStringAsync(
                key, payload, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }, ct);
        }
        catch
        {
            // Cache write failed — the freshly computed value is still returned.
        }

        return value;
    }
}
