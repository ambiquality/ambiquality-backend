using Ambiquality.Auth.Api.Application.Abstractions;
using Ambiquality.Auth.Api.Domain.Users;

namespace Ambiquality.Auth.Api.Application.Users;

/// <summary>
/// Permanently deletes a user's account after verifying the current password.
/// The removal cascades to the user's owned refresh and verification tokens, so
/// the account and all its sessions disappear in a single transaction. Evidence
/// keeps its lazily-projected <c>user_projection</c> row (no cross-database FK);
/// it is only an audit shadow and carries no credentials.
/// </summary>
public sealed class DeleteAccountHandler(
    IUserRepository repository,
    IPasswordService passwordService)
{
    public async Task HandleAsync(
        DeleteAccountCommand command, CancellationToken cancellationToken = default)
    {
        var user = await repository.GetByIdAsync(command.UserId, cancellationToken)
            ?? throw new UserNotFoundException();

        if (!passwordService.Verify(user, user.PasswordHash, command.CurrentPassword))
            throw new InvalidCredentialsException();

        repository.Remove(user);

        await repository.SaveChangesAsync(cancellationToken);
    }
}
