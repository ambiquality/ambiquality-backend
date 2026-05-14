namespace Ambiquality.Auth.Api.Application.Users;

/// <summary>Use-case inputs for the authentication and account handlers.</summary>
public sealed record RegisterUserCommand(string Email, string Password);

public sealed record LoginCommand(string Email, string Password);

public sealed record RefreshTokenCommand(string RefreshToken);

public sealed record ConfirmEmailCommand(Guid UserId, string Token);

public sealed record ResendConfirmationCommand(string Email);

public sealed record ChangePasswordCommand(Guid UserId, string CurrentPassword, string NewPassword);

public sealed record ChangeEmailCommand(Guid UserId, string NewEmail);

public sealed record ConfirmEmailChangeCommand(Guid UserId, string Token);
