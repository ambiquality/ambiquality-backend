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
}
