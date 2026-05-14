namespace Ambiquality.Auth.Api.Infrastructure.Security;

/// <summary>JWT signing and validation settings, bound from the <c>Jwt</c> config section.</summary>
public sealed class JwtOptions
{
    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    /// <summary>Symmetric HMAC-SHA256 signing secret. Must be at least 32 bytes.</summary>
    public string Secret { get; init; } = string.Empty;

    public int AccessTokenMinutes { get; init; } = 15;
}
