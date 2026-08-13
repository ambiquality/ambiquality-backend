using Ambiquality.Auth.Api.Application;

namespace Ambiquality.Auth.Api.Api;

/// <summary>
/// The refresh token is delivered as an <c>HttpOnly</c> cookie instead of a
/// response body so page JavaScript can never read it (XSS-proof, WSTG-SESS-04).
/// The API and the SPA share the same registrable domain
/// (api.ambiquality.org / ambiquality.org), so <c>SameSite=Strict</c> still sends
/// the cookie on the SPA's requests while blocking cross-site CSRF.
/// </summary>
public static class RefreshTokenCookie
{
    public const string Name = "amq_refresh";

    /// <summary>
    /// Cookie path. "/" keeps the cookie working whether the API is reached
    /// behind Caddy's /auth prefix (production/dev) or directly at /v1
    /// (integration tests); it is only ever read by the auth endpoints.
    /// </summary>
    public const string Path = "/";

    public static CookieOptions Options(bool secure, DateTime expiresAt) => new()
    {
        HttpOnly = true,
        Secure = secure,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        Expires = expiresAt
    };

    /// <summary>
    /// Clears the refresh cookie with the same path/attributes used to set it,
    /// so the browser actually drops it on logout.
    /// </summary>
    public static CookieOptions ClearOptions(bool secure) => new()
    {
        HttpOnly = true,
        Secure = secure,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        Expires = DateTimeOffset.UnixEpoch,
        MaxAge = TimeSpan.Zero
    };

    /// <summary>Writes the refresh cookie for a freshly issued token.</summary>
    public static void Append(HttpResponse response, AuthOptions options, string token, DateTime expiresAt)
        => response.Cookies.Append(Name, token, Options(options.RefreshCookieSecure, expiresAt));

    /// <summary>Clears the refresh cookie (logout).</summary>
    public static void Clear(HttpResponse response, AuthOptions options)
        => response.Cookies.Delete(Name, ClearOptions(options.RefreshCookieSecure));
}
