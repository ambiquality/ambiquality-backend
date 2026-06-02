using Ambiquality.Auth.Api.Domain.Users;

namespace Ambiquality.Auth.Api.Tests.TestSupport;

/// <summary>Hand-written in-memory <see cref="IUserRepository"/> for handler tests.</summary>
public sealed class InMemoryUserRepository : IUserRepository
{
    private readonly List<User> _users = [];

    public int SaveChangesCallCount { get; private set; }

    public IReadOnlyList<User> Users => _users;

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_users.FirstOrDefault(u => u.Id == id));

    public Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default)
        => Task.FromResult(_users.FirstOrDefault(u => u.Email.Equals(email)));

    public Task<User?> GetByRefreshTokenHashAsync(
        string refreshTokenHash, CancellationToken cancellationToken = default)
        => Task.FromResult(_users.FirstOrDefault(
            u => u.RefreshTokens.Any(t => t.TokenHash == refreshTokenHash)));

    public void Add(User user) => _users.Add(user);

    public void Remove(User user) => _users.Remove(user);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }

    /// <summary>Test helper: find a user by raw email string.</summary>
    public User? FindByEmail(string email)
        => _users.FirstOrDefault(u => u.Email.Value == email.ToLowerInvariant());
}
