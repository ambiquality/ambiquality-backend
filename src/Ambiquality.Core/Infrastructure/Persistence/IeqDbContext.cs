using Ambiquality.Core.Domain.Measurements;
using Microsoft.EntityFrameworkCore;

namespace Ambiquality.Core.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the time-series measurement database (<c>ieq</c>).
/// Owned by Ingestion.Api (read-write, runs migrations); referenced read-only
/// by the planned Public.Api. The <c>measurements</c> table is converted to a
/// TimescaleDB hypertable in the initial migration.
/// </summary>
public sealed class IeqDbContext(DbContextOptions<IeqDbContext> options) : DbContext(options)
{
    public DbSet<Measurement> Measurements => Set<Measurement>();
    public DbSet<ParameterRange> ParameterRanges => Set<ParameterRange>();
    public DbSet<MeasurementExport> MeasurementExports => Set<MeasurementExport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("ieq");
        modelBuilder.ApplyConfiguration(new MeasurementConfiguration());
        modelBuilder.ApplyConfiguration(new ParameterRangeConfiguration());
        modelBuilder.ApplyConfiguration(new MeasurementExportConfiguration());
    }
}
