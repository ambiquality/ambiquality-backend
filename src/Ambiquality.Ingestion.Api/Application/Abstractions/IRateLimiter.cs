namespace Ambiquality.Ingestion.Api.Application.Abstractions;

/// <summary>
/// A per-key fixed-window rate limiter. One ingestion call counts as one hit against
/// the sensor's window; the implementation is responsible for the atomic
/// "increment-and-expire" so concurrent calls cannot both slip past the limit.
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Records a hit against <paramref name="key"/> and reports whether it is within the
    /// allowance. A fresh key starts a new window of <paramref name="windowSeconds"/>;
    /// up to <paramref name="permitsPerWindow"/> hits are allowed before the window
    /// resets. When the allowance is exceeded the decision carries the seconds left in
    /// the current window (for a <c>Retry-After</c> header).
    /// </summary>
    Task<RateLimitDecision> HitAsync(string key, int windowSeconds, int permitsPerWindow, CancellationToken cancellationToken);
}

/// <param name="Allowed">True when the hit is within the allowance.</param>
/// <param name="RetryAfterSeconds">
/// When <paramref name="Allowed"/> is false, the whole seconds the caller should wait
/// before the window resets; zero otherwise.
/// </param>
public readonly record struct RateLimitDecision(bool Allowed, int RetryAfterSeconds)
{
    public static readonly RateLimitDecision Allow = new(true, 0);

    public static RateLimitDecision Deny(int retryAfterSeconds) => new(false, retryAfterSeconds);
}
