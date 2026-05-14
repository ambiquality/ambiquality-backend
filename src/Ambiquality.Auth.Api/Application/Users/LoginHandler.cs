using Ambiquality.Auth.Api.Application.Abstractions;
using Ambiquality.Auth.Api.Domain.Users;

namespace Ambiquality.Auth.Api.Application.Users;

/// <summary>
/// Authenticates a user by email + password and, on success, issues a JWT
/// access token plus a persisted refresh token.
/// </summary>
public sealed class LoginHandler(
    IUserRepository repository,
    IPasswordService passwordService,
    ITokenGenerator tokenGenerator,
    IJwtIssuer jwtIssuer,
    IClock clock,
    AuthOptions options)
{
    public async Task<AuthResult> HandleAsync(
        LoginCommand command, CancellationToken cancellationToken = default)
    {
        var email = Email.Create(command.Email);

        var user = await repository.GetByEmailAsync(email, cancellationToken)
            ?? throw new InvalidCredentialsException();

        if (!passwordService.Verify(user, user.PasswordHash, command.Password))
            throw new InvalidCredentialsException();

        if (!user.EmailConfirmed)
            throw new EmailNotConfirmedException();

        var refresh = tokenGenerator.Generate();
        user.IssueRefreshToken(refresh.TokenHash, clock.UtcNow, options.RefreshTokenLifetime);
        await repository.SaveChangesAsync(cancellationToken);

        var access = jwtIssuer.Issue(user);
        return new AuthResult(
            access.Value,
            access.ExpiresAt,
            refresh.RawToken,
            clock.UtcNow + options.RefreshTokenLifetime);
    }
}
