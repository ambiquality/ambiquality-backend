using Ambiquality.Auth.Api.Application.Abstractions;
using Ambiquality.Auth.Api.Domain;
using Ambiquality.Auth.Api.Domain.Users;

namespace Ambiquality.Auth.Api.Application.Users;

/// <summary>
/// Exchanges an active refresh token for a fresh JWT + refresh token, revoking
/// the presented token (rotation).
/// </summary>
public sealed class RefreshTokenHandler(
    IUserRepository repository,
    ITokenGenerator tokenGenerator,
    IJwtIssuer jwtIssuer,
    IClock clock,
    AuthOptions options)
{
    public async Task<AuthResult> HandleAsync(
        RefreshTokenCommand command, CancellationToken cancellationToken = default)
    {
        var presentedHash = tokenGenerator.Hash(command.RefreshToken);

        var user = await repository.GetByRefreshTokenHashAsync(presentedHash, cancellationToken)
            ?? throw new InvalidRefreshTokenException();

        var fresh = tokenGenerator.Generate();
        try
        {
            user.RotateRefreshToken(
                presentedHash, fresh.TokenHash, clock.UtcNow, options.RefreshTokenLifetime);
        }
        catch (DomainException)
        {
            throw new InvalidRefreshTokenException();
        }

        await repository.SaveChangesAsync(cancellationToken);

        var access = jwtIssuer.Issue(user);
        return new AuthResult(
            access.Value,
            access.ExpiresAt,
            fresh.RawToken,
            clock.UtcNow + options.RefreshTokenLifetime);
    }
}
