using Ambiquality.Auth.Api.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Ambiquality.Auth.Api.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the <c>auth</c> database. Deliberately a plain
/// <see cref="DbContext"/> — not <c>IdentityDbContext</c> — so the domain model
/// stays fully owned by this service.
/// </summary>
public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("auth");
        modelBuilder.ApplyConfiguration(new UserConfiguration());
    }
}
