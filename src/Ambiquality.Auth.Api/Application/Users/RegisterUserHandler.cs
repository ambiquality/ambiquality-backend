using Ambiquality.Auth.Api.Application.Abstractions;
using Ambiquality.Auth.Api.Domain.Users;

namespace Ambiquality.Auth.Api.Application.Users;

/// <summary>
/// Registers a new user: creates the aggregate, hashes the password, mints a
/// confirmation token, persists, and emails the confirmation link. Never logs
/// the user in.
/// </summary>
public sealed class RegisterUserHandler(
    IUserRepository repository,
    IPasswordService passwordService,
    ITokenGenerator tokenGenerator,
    IEmailSender emailSender,
    IClock clock,
    AuthOptions options)
{
    public async Task HandleAsync(RegisterUserCommand command, CancellationToken cancellationToken = default)
    {
        var email = Email.Create(command.Email);

        if (await repository.GetByEmailAsync(email, cancellationToken) is not null)
            throw new EmailAlreadyRegisteredException();

        var token = tokenGenerator.Generate();

        // Hash needs a User instance for IPasswordHasher<User>; build it first
        // with a placeholder, then assign the real hash.
        var user = User.Register(
            email,
            passwordHash: "pending",
            confirmationTokenHash: token.TokenHash,
            now: clock.UtcNow,
            confirmationTokenLifetime: options.ConfirmationTokenLifetime);
        user.ChangePassword(passwordService.Hash(user, command.Password));

        repository.Add(user);
        await repository.SaveChangesAsync(cancellationToken);

        var (subject, body) = ConfirmationEmail.ForRegistration(
            options.FrontendBaseUrl, user.Id, token.RawToken);
        await emailSender.SendAsync(email.Value, subject, body, cancellationToken);
    }
}
