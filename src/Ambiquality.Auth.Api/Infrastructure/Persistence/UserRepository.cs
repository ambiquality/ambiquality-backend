using Ambiquality.Auth.Api.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Ambiquality.Auth.Api.Infrastructure.Persistence;

/// <summary>
/// EF Core-backed <see cref="IUserRepository"/>. Owned token collections load
/// automatically with the aggregate, so no explicit Include is required.
/// </summary>
public sealed class UserRepository(AuthDbContext dbContext) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => dbContext.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default)
        => dbContext.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<User?> GetByRefreshTokenHashAsync(
        string refreshTokenHash, CancellationToken cancellationToken = default)
        => dbContext.Users.FirstOrDefaultAsync(
            u => EF.Property<IReadOnlyCollection<RefreshToken>>(u, "RefreshTokens")
                .Any(t => t.TokenHash == refreshTokenHash),
            cancellationToken);

    public void Add(User user) => dbContext.Users.Add(user);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
