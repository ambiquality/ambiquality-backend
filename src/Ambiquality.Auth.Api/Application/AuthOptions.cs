using Ambiquality.Auth.Api.Domain.Users;

namespace Ambiquality.Auth.Api.Application;

/// <summary>
/// Token lifetimes and related knobs shared by the application handlers.
/// Bound from configuration in Program.cs.
/// </summary>
public sealed class AuthOptions
{
    public TimeSpan ConfirmationTokenLifetime { get; init; } = TimeSpan.FromHours(24);

    public TimeSpan EmailChangeTokenLifetime { get; init; } = TimeSpan.FromHours(24);

    public TimeSpan RefreshTokenLifetime { get; init; } = TimeSpan.FromDays(30);

    /// <summary>Base URL the frontend uses to build confirmation links in emails.</summary>
    public string FrontendBaseUrl { get; init; } = "https://localhost";

    /// <summary>
    /// Marks the refresh-token cookie <c>Secure</c>. Must be <c>true</c> in production
    /// (HTTPS only); <c>false</c> in dev where the stack runs over plain HTTP.
    /// </summary>
    public bool RefreshCookieSecure { get; init; } = false;

    // --- Brute-force throttling (no account lockout; see OWASP) --------------

    /// <summary>Failed logins per account allowed before the backoff delay starts.</summary>
    public int LoginThrottleFreeAttempts { get; init; } = 5;

    /// <summary>First non-zero backoff delay; doubles each further failure.</summary>
    public TimeSpan LoginThrottleBaseDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>Upper bound on the per-account backoff delay.</summary>
    public TimeSpan LoginThrottleMaxDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>A failure streak idle longer than this is forgotten.</summary>
    public TimeSpan LoginThrottleResetWindow { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>Failed/total login attempts permitted per client IP per <see cref="LoginIpWindow"/>.</summary>
    public int LoginIpPermitLimit { get; init; } = 10;

    /// <summary>Fixed window for the per-IP login rate limit.</summary>
    public TimeSpan LoginIpWindow { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Per-account backoff policy assembled from the throttle knobs.</summary>
    public LoginThrottlePolicy LoginThrottlePolicy => new(
        LoginThrottleFreeAttempts,
        LoginThrottleBaseDelay,
        LoginThrottleMaxDelay,
        LoginThrottleResetWindow);
}
