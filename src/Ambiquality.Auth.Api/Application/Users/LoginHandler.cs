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
    IThrottleDelayer throttleDelayer,
    AuthOptions options)
{
    public async Task<AuthResult> HandleAsync(
        LoginCommand command, CancellationToken cancellationToken = default)
    {
        var email = Email.Create(command.Email);

        var user = await repository.GetByEmailAsync(email, cancellationToken)
            ?? throw new InvalidCredentialsException();

        // Per-account progressive backoff: slow repeated guessing against THIS
        // account without ever locking it out (a lockout would let an attacker
        // deny service to the real user). Unknown emails have no row to track, so
        // volumetric guessing is left to the per-IP rate limiter on the endpoint.
        var delay = user.ThrottleDelay(clock.UtcNow, options.LoginThrottlePolicy);
        if (delay > TimeSpan.Zero)
            await throttleDelayer.DelayAsync(delay, cancellationToken);

        if (!passwordService.Verify(user, user.PasswordHash, command.Password))
        {
            user.RegisterFailedLogin(clock.UtcNow, options.LoginThrottleResetWindow);
            await repository.SaveChangesAsync(cancellationToken);
            throw new InvalidCredentialsException();
        }

        if (!user.EmailConfirmed)
            throw new EmailNotConfirmedException();

        // Correct password — clear the failure streak so the next login is instant.
        user.RegisterSuccessfulLogin();
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
