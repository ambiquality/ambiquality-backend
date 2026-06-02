namespace Ambiquality.Auth.Api.Domain.Users;

/// <summary>
/// Persistence port for the <see cref="User"/> aggregate. The implementation
/// lives in the Infrastructure layer.
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default);

    /// <summary>Finds the user that owns the refresh token with the given hash, if any.</summary>
    Task<User?> GetByRefreshTokenHashAsync(
        string refreshTokenHash, CancellationToken cancellationToken = default);

    void Add(User user);

    /// <summary>
    /// Removes the user aggregate. Owned refresh and verification tokens are
    /// deleted along with it (cascade), so account deletion is a single unit.
    /// </summary>
    void Remove(User user);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
