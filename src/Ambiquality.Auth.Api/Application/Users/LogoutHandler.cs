using Ambiquality.Auth.Api.Application.Abstractions;
using Ambiquality.Auth.Api.Domain.Users;

namespace Ambiquality.Auth.Api.Application.Users;

/// <summary>
/// Revokes all refresh tokens for the acting user, preventing any further
/// silent re-authentication from this session. The short-lived JWT access
/// token remains valid until its natural expiry.
/// </summary>
public sealed class LogoutHandler(IUserRepository repository, IClock clock)
{
    public async Task HandleAsync(LogoutCommand command, CancellationToken cancellationToken = default)
    {
        var user = await repository.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null) return;

        user.RevokeAllRefreshTokens(clock.UtcNow);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
