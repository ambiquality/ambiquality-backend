using Ambiquality.Auth.Api.Application.Abstractions;
using Ambiquality.Auth.Api.Domain.Users;

namespace Ambiquality.Auth.Api.Application.Users;

/// <summary>
/// Starts an email-change flow: records the pending address and emails a
/// confirmation token to the NEW address.
/// </summary>
public sealed class ChangeEmailHandler(
    IUserRepository repository,
    ITokenGenerator tokenGenerator,
    IEmailSender emailSender,
    IClock clock,
    AuthOptions options)
{
    public async Task HandleAsync(
        ChangeEmailCommand command, CancellationToken cancellationToken = default)
    {
        var newEmail = Email.Create(command.NewEmail);

        var user = await repository.GetByIdAsync(command.UserId, cancellationToken)
            ?? throw new UserNotFoundException();

        var token = tokenGenerator.Generate();
        user.RequestEmailChange(
            newEmail, token.TokenHash, clock.UtcNow, options.EmailChangeTokenLifetime);
        await repository.SaveChangesAsync(cancellationToken);

        var (subject, body) = ConfirmationEmail.ForEmailChange(
            options.FrontendBaseUrl, user.Id, token.RawToken);
        await emailSender.SendAsync(newEmail.Value, subject, body, cancellationToken);
    }
}
