namespace Ambiquality.Auth.Api.Domain.Users;

/// <summary>
/// A single-use, crypto-random token for email confirmation flows. Only the
/// SHA-256 hash of the raw token is stored.
/// </summary>
public sealed class VerificationToken
{
    private VerificationToken(
        string tokenHash, VerificationPurpose purpose, DateTime createdAt, DateTime expiresAt)
    {
        Id = Guid.NewGuid();
        TokenHash = tokenHash;
        Purpose = purpose;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        ConsumedAt = null;
    }

    // Parameterless constructor for EF Core materialization.
    private VerificationToken()
    {
        TokenHash = null!;
    }

    public Guid Id { get; private set; }
    public string TokenHash { get; private set; }
    public VerificationPurpose Purpose { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? ConsumedAt { get; private set; }

    public static VerificationToken Issue(
        string tokenHash, VerificationPurpose purpose, DateTime now, TimeSpan lifetime)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new DomainException("Verification token hash cannot be empty.");
        if (lifetime <= TimeSpan.Zero)
            throw new DomainException("Verification token lifetime must be positive.");

        return new VerificationToken(tokenHash, purpose, now, now + lifetime);
    }

    /// <summary>
    /// Returns true when the supplied hash matches, the purpose matches, the
    /// token has not been consumed, and it has not expired.
    /// </summary>
    public bool Validate(string tokenHash, VerificationPurpose purpose, DateTime now)
    {
        return TokenHash == tokenHash
            && Purpose == purpose
            && ConsumedAt is null
            && now < ExpiresAt;
    }

    public void Consume(DateTime now)
    {
        if (ConsumedAt is not null)
            throw new DomainException("Verification token has already been consumed.");

        ConsumedAt = now;
    }
}
