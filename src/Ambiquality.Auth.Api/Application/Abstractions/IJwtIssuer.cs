using Ambiquality.Auth.Api.Domain.Users;

namespace Ambiquality.Auth.Api.Application.Abstractions;

/// <summary>An issued access token and the instant it expires.</summary>
public sealed record AccessToken(string Value, DateTime ExpiresAt);

/// <summary>Issues signed JWT access tokens for a user.</summary>
public interface IJwtIssuer
{
    AccessToken Issue(User user);
}
