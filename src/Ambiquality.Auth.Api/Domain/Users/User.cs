namespace Ambiquality.Auth.Api.Domain.Users;

/// <summary>
/// Aggregate root for an authenticated identity. Owns its refresh tokens and
/// verification tokens; all state transitions go through behavior methods so
/// invariants are enforced in one place.
/// </summary>
public sealed class User
{
    private readonly List<RefreshToken> _refreshTokens = [];
    private readonly List<VerificationToken> _verificationTokens = [];

    private User(Guid id, Email email, string passwordHash)
    {
        Id = id;
        Email = email;
        PasswordHash = passwordHash;
        EmailConfirmed = false;
        PendingEmail = null;
    }

    // Parameterless constructor for EF Core materialization.
    private User()
    {
        Email = null!;
        PasswordHash = null!;
    }

    public Guid Id { get; private set; }
    public Email Email { get; private set; }
    public bool EmailConfirmed { get; private set; }
    public string PasswordHash { get; private set; }
    public Email? PendingEmail { get; private set; }

    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();
    public IReadOnlyCollection<VerificationToken> VerificationTokens => _verificationTokens.AsReadOnly();

    /// <summary>
    /// Creates a new, unconfirmed user and mints the initial email-confirmation
    /// token. Registration never logs the user in.
    /// </summary>
    public static User Register(
        Email email,
        string passwordHash,
        string confirmationTokenHash,
        DateTime now,
        TimeSpan confirmationTokenLifetime)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("Password hash cannot be empty.");

        var user = new User(Guid.NewGuid(), email, passwordHash);
        user._verificationTokens.Add(VerificationToken.Issue(
            confirmationTokenHash, VerificationPurpose.EmailConfirmation, now, confirmationTokenLifetime));
        return user;
    }

    /// <summary>Issues an additional email-confirmation token (resend flow).</summary>
    public void AddConfirmationToken(string tokenHash, DateTime now, TimeSpan lifetime)
    {
        if (EmailConfirmed)
            throw new DomainException("Email address is already confirmed.");

        _verificationTokens.Add(VerificationToken.Issue(
            tokenHash, VerificationPurpose.EmailConfirmation, now, lifetime));
    }

    public void ConfirmEmail(string rawTokenHash, DateTime now)
    {
        if (EmailConfirmed)
            throw new DomainException("Email address is already confirmed.");

        var token = FindValidToken(rawTokenHash, VerificationPurpose.EmailConfirmation, now);
        token.Consume(now);
        EmailConfirmed = true;
    }

    public void ChangePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new DomainException("Password hash cannot be empty.");

        PasswordHash = newPasswordHash;
    }

    /// <summary>
    /// Starts an email-change flow: records the pending address and mints a
    /// confirmation token that must be delivered to the NEW address.
    /// </summary>
    public void RequestEmailChange(
        Email newEmail, string tokenHash, DateTime now, TimeSpan lifetime)
    {
        if (!EmailConfirmed)
            throw new DomainException("Current email address must be confirmed first.");
        if (newEmail.Equals(Email))
            throw new DomainException("New email address must differ from the current one.");

        PendingEmail = newEmail;
        _verificationTokens.Add(VerificationToken.Issue(
            tokenHash, VerificationPurpose.EmailChange, now, lifetime));
    }

    public void ConfirmEmailChange(string rawTokenHash, DateTime now)
    {
        if (PendingEmail is null)
            throw new DomainException("There is no pending email change to confirm.");

        var token = FindValidToken(rawTokenHash, VerificationPurpose.EmailChange, now);
        token.Consume(now);
        Email = PendingEmail;
        PendingEmail = null;
    }

    public void IssueRefreshToken(string tokenHash, DateTime now, TimeSpan lifetime)
    {
        _refreshTokens.Add(RefreshToken.Issue(tokenHash, now, lifetime));
    }

    /// <summary>
    /// Revokes the supplied (active) refresh token and issues a fresh one in a
    /// single atomic transition.
    /// </summary>
    public void RotateRefreshToken(
        string oldTokenHash, string newTokenHash, DateTime now, TimeSpan lifetime)
    {
        var existing = _refreshTokens.FirstOrDefault(t => t.TokenHash == oldTokenHash);
        if (existing is null || !existing.IsActive(now))
            throw new DomainException("Refresh token is invalid or no longer active.");

        existing.Revoke(now);
        _refreshTokens.Add(RefreshToken.Issue(newTokenHash, now, lifetime));
    }

    public void RevokeAllRefreshTokens(DateTime now)
    {
        foreach (var token in _refreshTokens)
            token.Revoke(now);
    }

    private VerificationToken FindValidToken(
        string tokenHash, VerificationPurpose purpose, DateTime now)
    {
        var token = _verificationTokens.FirstOrDefault(t => t.Validate(tokenHash, purpose, now));
        if (token is null)
            throw new DomainException("Verification token is invalid, expired, or already used.");

        return token;
    }
}
