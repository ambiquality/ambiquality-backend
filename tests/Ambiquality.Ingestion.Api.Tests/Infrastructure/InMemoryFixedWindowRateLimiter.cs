using System.Collections.Concurrent;
using Ambiquality.Ingestion.Api.Application.Abstractions;

namespace Ambiquality.Ingestion.Api.Tests.Infrastructure;

/// <summary>
/// In-memory stand-in for <c>RedisFixedWindowRateLimiter</c> so the endpoint tests need
/// no Redis broker. Mirrors the same fixed-window semantics: the first hit opens a window
/// and stamps its expiry; later hits in the window increment a counter and are denied once
/// the allowance is exceeded, reporting the seconds left. A small <see cref="Clock"/> hook
/// lets a test advance time to prove the window resets.
/// </summary>
public sealed class InMemoryFixedWindowRateLimiter : IRateLimiter
{
    private sealed record Window(DateTimeOffset ExpiresAt, int Count);

    private readonly ConcurrentDictionary<string, Window> _windows = new();

    /// <summary>Overridable clock; defaults to wall time.</summary>
    public Func<DateTimeOffset> Clock { get; set; } = () => DateTimeOffset.UtcNow;

    public Task<RateLimitDecision> HitAsync(
        string key, int windowSeconds, int permitsPerWindow, CancellationToken cancellationToken)
    {
        var now = Clock();
        var window = _windows.AddOrUpdate(
            key,
            _ => new Window(now.AddSeconds(windowSeconds), 1),
            (_, existing) => existing.ExpiresAt <= now
                ? new Window(now.AddSeconds(windowSeconds), 1)   // previous window elapsed → reset
                : existing with { Count = existing.Count + 1 });

        if (window.Count <= permitsPerWindow)
            return Task.FromResult(RateLimitDecision.Allow);

        var retryAfter = (int)Math.Ceiling((window.ExpiresAt - now).TotalSeconds);
        return Task.FromResult(RateLimitDecision.Deny(Math.Max(retryAfter, 1)));
    }
}
