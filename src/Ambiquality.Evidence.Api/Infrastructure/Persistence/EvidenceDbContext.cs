using Ambiquality.Evidence.Api.Domain.Buildings;
using Ambiquality.Evidence.Api.Domain.Rooms;
using Ambiquality.Evidence.Api.Domain.Sensors;
using Ambiquality.Evidence.Api.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Ambiquality.Evidence.Api.Infrastructure.Persistence;

public sealed class EvidenceDbContext(DbContextOptions<EvidenceDbContext> options) : DbContext(options)
{
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Sensor> Sensors => Set<Sensor>();
    public DbSet<UserProjection> UserProjections => Set<UserProjection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("btree_gist");
        modelBuilder.HasDefaultSchema("evidence");
        modelBuilder.ApplyConfiguration(new BuildingConfiguration());
        modelBuilder.ApplyConfiguration(new RoomConfiguration());
        modelBuilder.ApplyConfiguration(new SensorConfiguration());
        modelBuilder.ApplyConfiguration(new UserProjectionConfiguration());
    }
}
