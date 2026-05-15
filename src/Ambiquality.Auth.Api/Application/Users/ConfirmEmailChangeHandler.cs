using Ambiquality.Auth.Api.Application.Abstractions;
using Ambiquality.Auth.Api.Domain.Users;

namespace Ambiquality.Auth.Api.Application.Users;

/// <summary>Applies a pending email change once the new address is confirmed.</summary>
public sealed class ConfirmEmailChangeHandler(
    IUserRepository repository,
    ITokenGenerator tokenGenerator,
    IClock clock)
{
    public async Task HandleAsync(
        ConfirmEmailChangeCommand command, CancellationToken cancellationToken = default)
    {
        var user = await repository.GetByIdAsync(command.UserId, cancellationToken)
            ?? throw new UserNotFoundException();

        var tokenHash = tokenGenerator.Hash(command.Token);
        user.ConfirmEmailChange(tokenHash, clock.UtcNow);

        await repository.SaveChangesAsync(cancellationToken);
    }
}
