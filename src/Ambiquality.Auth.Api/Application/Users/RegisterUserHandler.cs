using Ambiquality.Auth.Api.Application.Abstractions;
using Ambiquality.Auth.Api.Domain.Users;

namespace Ambiquality.Auth.Api.Application.Users;

/// <summary>
/// Registers a new user: creates the aggregate, hashes the password, mints a
/// confirmation token, persists, and emails the confirmation link. Never logs
/// the user in. An already-registered address is a silent no-op (uniform 201)
/// so the endpoint cannot be used to enumerate accounts.
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

        // Validate the password FIRST so the response never depends on whether the
        // email already exists (anti-enumeration — a weak-password 400 is uniform).
        PasswordPolicy.Validate(command.Password, options.PasswordMinLength, options.PasswordMaxLength);

        // Anti-enumeration: an existing address is a silent no-op. Same 201 response
        // as a fresh registration, no second row, no email. Users who need a fresh
        // confirmation link use POST /resend-confirmation (also always-202).
        if (await repository.GetByEmailAsync(email, cancellationToken) is not null)
            return;

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
