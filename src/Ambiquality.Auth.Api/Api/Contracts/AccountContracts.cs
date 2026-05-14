namespace Ambiquality.Auth.Api.Api.Contracts;

/// <summary>Request DTOs for the authenticated account endpoints.</summary>
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record ChangeEmailRequest(string NewEmail);
