namespace Ambiquality.Auth.Api.Application.Users;

/// <summary>
/// Shared password-length policy enforced on registration and password change.
/// Deliberately length-only (NIST SP 800-63B): no composition rules such as
/// mandatory upper/lower/digit/symbol, which add friction without real strength.
/// The limits are configurable via <see cref="AuthOptions"/>.
/// </summary>
public static class PasswordPolicy
{
    public static void Validate(string password, int minLength, int maxLength)
    {
        if (string.IsNullOrEmpty(password) || password.Length < minLength)
            throw new WeakPasswordException($"Password must be at least {minLength} characters.");

        if (password.Length > maxLength)
            throw new WeakPasswordException($"Password must be at most {maxLength} characters.");
    }
}
