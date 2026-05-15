namespace Ambiquality.Auth.Api.Api.Contracts;

/// <summary>Request and response DTOs for the authentication endpoints.</summary>
public sealed record RegisterRequest(string Email, string Password);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record ResendConfirmationRequest(string Email);

/// <summary>Returned on successful login or refresh.</summary>
public sealed record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt);

/// <summary>Identity projection returned by <c>GET /me</c>.</summary>
public sealed record MeResponse(Guid Id, string Email, bool EmailConfirmed);
