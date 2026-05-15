namespace Ambiquality.Auth.Api.Domain.Users;

/// <summary>
/// A refresh token belonging to a <see cref="User"/>. Only the SHA-256 hash of
/// the raw token is stored; the raw value is returned to the client once.
/// </summary>
public sealed class RefreshToken
{
    private RefreshToken(string tokenHash, DateTime createdAt, DateTime expiresAt)
    {
        Id = Guid.NewGuid();
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        RevokedAt = null;
    }

    // Parameterless constructor for EF Core materialization.
    private RefreshToken()
    {
        TokenHash = null!;
    }

    public Guid Id { get; private set; }
    public string TokenHash { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    public static RefreshToken Issue(string tokenHash, DateTime now, TimeSpan lifetime)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new DomainException("Refresh token hash cannot be empty.");
        if (lifetime <= TimeSpan.Zero)
            throw new DomainException("Refresh token lifetime must be positive.");

        return new RefreshToken(tokenHash, now, now + lifetime);
    }

    public bool IsActive(DateTime now)
    {
        return RevokedAt is null && now < ExpiresAt;
    }

    public void Revoke(DateTime now)
    {
        RevokedAt ??= now;
    }
}
