namespace Ambiquality.Auth.Api.Domain.Users;

/// <summary>
/// Parameters for the per-account login backoff. <paramref name="FreeAttempts"/>
/// failures are allowed with no delay; beyond that the delay doubles each attempt
/// (<paramref name="BaseDelay"/> × 2ⁿ) up to <paramref name="MaxDelay"/>. A streak
/// idle longer than <paramref name="ResetWindow"/> is forgotten.
/// </summary>
public sealed record LoginThrottlePolicy(
    int FreeAttempts,
    TimeSpan BaseDelay,
    TimeSpan MaxDelay,
    TimeSpan ResetWindow);
