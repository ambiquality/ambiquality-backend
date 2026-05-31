using Ambiquality.Core.Domain.Measurements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ambiquality.Core.Infrastructure.Persistence;

public sealed class ParameterRangeConfiguration : IEntityTypeConfiguration<ParameterRange>
{
    public void Configure(EntityTypeBuilder<ParameterRange> builder)
    {
        builder.ToTable("parameter_ranges");
        builder.HasKey(p => p.ParameterCode);

        builder.Property(p => p.ParameterCode)
            .HasColumnName("parameter_code")
            .HasMaxLength(50)
            .ValueGeneratedNever();

        builder.Property(p => p.MinValue)
            .HasColumnName("min_value")
            .IsRequired();

        builder.Property(p => p.MaxValue)
            .HasColumnName("max_value")
            .IsRequired();

        builder.Property(p => p.Unit)
            .HasColumnName("unit")
            .HasMaxLength(50);

        // Sensor-domain validity bounds for all 18 IEQ quantities the platform tracks.
        // These are conservative physical sensor ranges, not health guidelines.
        // Units match the QUDT unit URIs in QudtVocabulary (Evidence.Api).
        builder.HasData(
            // Gases — ppm
            new ParameterRange("co2",          0,       50_000, "ppm"),
            new ParameterRange("eco2",         0,       65_000, "ppm"),
            new ParameterRange("co",           0,        2_000, "ppm"),

            // Gases — µg/m³ (European standard units for outdoor-origin pollutants)
            new ParameterRange("o3",           0,          500, "µg/m³"),
            new ParameterRange("no2",          0,          500, "µg/m³"),
            new ParameterRange("so2",          0,          500, "µg/m³"),

            // VOC — ppb
            new ParameterRange("voc",          0,       60_000, "ppb"),

            // Particulate matter — µg/m³ (PM10 can reach higher outdoors)
            new ParameterRange("pm1",          0,          500, "µg/m³"),
            new ParameterRange("pm2_5",        0,          500, "µg/m³"),
            new ParameterRange("pm4",          0,        1_000, "µg/m³"),
            new ParameterRange("pm10",         0,        1_000, "µg/m³"),

            // Thermal comfort
            new ParameterRange("temperature",  -40,          85, "°C"),
            new ParameterRange("humidity",       0,         100, "%"),
            new ParameterRange("air_velocity",   0,          10, "m/s"),
            new ParameterRange("pressure",  85_000,     110_000, "Pa"),

            // Light
            new ParameterRange("illuminance",    0,     100_000, "lx"),
            new ParameterRange("cct",        1_000,      20_000, "K"),

            // Acoustics — A-weighted equivalent continuous sound level
            new ParameterRange("laeq",           0,         140, "dB(A)"));
    }
}
