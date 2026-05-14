namespace Ambiquality.Auth.Api.Domain.Users;

/// <summary>
/// Distinguishes what a <see cref="VerificationToken"/> authorizes, so a token
/// minted for one flow cannot be replayed against another.
/// </summary>
public enum VerificationPurpose
{
    EmailConfirmation = 1,
    EmailChange = 2
}
