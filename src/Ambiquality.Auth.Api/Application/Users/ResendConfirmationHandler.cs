using Ambiquality.Auth.Api.Application.Abstractions;
using Ambiquality.Auth.Api.Domain.Users;

namespace Ambiquality.Auth.Api.Application.Users;

/// <summary>
/// Re-issues an email-confirmation token for an unconfirmed account. Silently
/// no-ops for unknown or already-confirmed accounts to avoid enumeration.
/// </summary>
public sealed class ResendConfirmationHandler(
    IUserRepository repository,
    ITokenGenerator tokenGenerator,
    IEmailSender emailSender,
    IClock clock,
    AuthOptions options)
{
    public async Task HandleAsync(
        ResendConfirmationCommand command, CancellationToken cancellationToken = default)
    {
        var email = Email.Create(command.Email);

        var user = await repository.GetByEmailAsync(email, cancellationToken);
        if (user is null || user.EmailConfirmed)
            return;

        var token = tokenGenerator.Generate();
        user.AddConfirmationToken(token.TokenHash, clock.UtcNow, options.ConfirmationTokenLifetime);
        await repository.SaveChangesAsync(cancellationToken);

        var (subject, body) = ConfirmationEmail.ForRegistration(
            options.FrontendBaseUrl, user.Id, token.RawToken);
        await emailSender.SendAsync(email.Value, subject, body, cancellationToken);
    }
}
