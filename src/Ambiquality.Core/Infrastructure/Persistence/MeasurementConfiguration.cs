using Ambiquality.Core.Domain.Measurements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ambiquality.Core.Infrastructure.Persistence;

public sealed class MeasurementConfiguration : IEntityTypeConfiguration<Measurement>
{
    public void Configure(EntityTypeBuilder<Measurement> builder)
    {
        // Composite key includes the hypertable partitioning column (received_at):
        // TimescaleDB requires any unique constraint to contain the partitioning key.
        builder.ToTable("measurements");
        builder.HasKey(m => new { m.Id, m.ReceivedAt });

        builder.Property(m => m.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(m => m.SensorId)
            .HasColumnName("sensor_id")
            .IsRequired();

        builder.Property(m => m.ParameterCode)
            .HasColumnName("parameter_code")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(m => m.Value)
            .HasColumnName("value")
            .IsRequired();

        builder.Property(m => m.Unit)
            .HasColumnName("unit")
            .HasMaxLength(50);

        builder.Property(m => m.ObservedAt)
            .HasColumnName("observed_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(m => m.ReceivedAt)
            .HasColumnName("received_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(m => m.IsInvalid)
            .HasColumnName("is_invalid")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(m => m.InvalidatedReason)
            .HasColumnName("invalidated_reason")
            .HasMaxLength(500);

        // Common read pattern: a sensor's readings for a quantity over time.
        builder.HasIndex(m => new { m.SensorId, m.ParameterCode, m.ReceivedAt })
            .HasDatabaseName("IX_measurements_sensor_parameter_time");
    }
}
