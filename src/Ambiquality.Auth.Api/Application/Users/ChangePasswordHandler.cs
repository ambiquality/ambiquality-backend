using Ambiquality.Auth.Api.Application.Abstractions;
using Ambiquality.Auth.Api.Domain.Users;

namespace Ambiquality.Auth.Api.Application.Users;

/// <summary>
/// Changes a user's password after verifying the current one, then revokes all
/// refresh tokens so existing sessions are forced to re-authenticate.
/// </summary>
public sealed class ChangePasswordHandler(
    IUserRepository repository,
    IPasswordService passwordService,
    IClock clock,
    AuthOptions options)
{
    public async Task HandleAsync(
        ChangePasswordCommand command, CancellationToken cancellationToken = default)
    {
        var user = await repository.GetByIdAsync(command.UserId, cancellationToken)
            ?? throw new UserNotFoundException();

        if (!passwordService.Verify(user, user.PasswordHash, command.CurrentPassword))
            throw new InvalidCredentialsException();

        PasswordPolicy.Validate(command.NewPassword, options.PasswordMinLength, options.PasswordMaxLength);

        user.ChangePassword(passwordService.Hash(user, command.NewPassword));
        user.RevokeAllRefreshTokens(clock.UtcNow);

        await repository.SaveChangesAsync(cancellationToken);
    }
}
