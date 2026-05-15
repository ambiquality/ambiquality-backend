using Ambiquality.Auth.Api.Application.Abstractions;
using Ambiquality.Auth.Api.Domain.Users;

namespace Ambiquality.Auth.Api.Application.Users;

/// <summary>Confirms a user's email address using a registration token.</summary>
public sealed class ConfirmEmailHandler(
    IUserRepository repository,
    ITokenGenerator tokenGenerator,
    IClock clock)
{
    public async Task HandleAsync(
        ConfirmEmailCommand command, CancellationToken cancellationToken = default)
    {
        var user = await repository.GetByIdAsync(command.UserId, cancellationToken)
            ?? throw new UserNotFoundException();

        var tokenHash = tokenGenerator.Hash(command.Token);
        user.ConfirmEmail(tokenHash, clock.UtcNow);

        await repository.SaveChangesAsync(cancellationToken);
    }
}
