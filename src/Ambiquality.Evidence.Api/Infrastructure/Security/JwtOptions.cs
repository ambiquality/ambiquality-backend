namespace Ambiquality.Evidence.Api.Infrastructure.Security;

/// <summary>
/// JWT validation settings, bound from the <c>Jwt</c> config section. Evidence.Api
/// only validates tokens (Auth.Api issues them), so it needs the issuer, audience
/// and the shared symmetric signing secret — they must match Auth.Api's values.
/// </summary>
public sealed class JwtOptions
{
    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    /// <summary>Symmetric HMAC-SHA256 signing secret shared with Auth.Api.</summary>
    public string Secret { get; init; } = string.Empty;
}
