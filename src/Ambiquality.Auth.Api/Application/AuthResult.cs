namespace Ambiquality.Auth.Api.Application;

/// <summary>
/// The outcome of a successful login or refresh: a short-lived access token
/// plus a long-lived refresh token (raw values, returned to the client once).
/// </summary>
public sealed record AuthResult(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt);
