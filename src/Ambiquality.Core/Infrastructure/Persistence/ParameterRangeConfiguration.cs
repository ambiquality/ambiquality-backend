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

        // Seed the seven IEQ quantities the platform tracks. Ranges are sane
        // sensor-domain bounds; tune in the table without a redeploy. Units are
        // informational until F08 unit matching lands.
        builder.HasData(
            new ParameterRange("co2", 0, 50_000, "ppm"),
            new ParameterRange("temperature", -40, 85, "°C"),
            new ParameterRange("humidity", 0, 100, "%"),
            new ParameterRange("pm", 0, 1_000, "µg/m³"),
            new ParameterRange("voc", 0, 60_000, "ppb"),
            new ParameterRange("acoustics", 0, 140, "dB"),
            new ParameterRange("light", 0, 100_000, "lx"));
    }
}
