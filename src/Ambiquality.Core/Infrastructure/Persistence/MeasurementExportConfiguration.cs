using Ambiquality.Core.Domain.Measurements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ambiquality.Core.Infrastructure.Persistence;

public sealed class MeasurementExportConfiguration : IEntityTypeConfiguration<MeasurementExport>
{
    public void Configure(EntityTypeBuilder<MeasurementExport> builder)
    {
        builder.ToTable("measurement_exports");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.Year)
            .HasColumnName("year")
            .IsRequired();

        builder.Property(e => e.Month)
            .HasColumnName("month")
            .IsRequired();

        builder.Property(e => e.MediaType)
            .HasColumnName("media_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.CompressFormat)
            .HasColumnName("compress_format")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.FileKey)
            .HasColumnName("file_key")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.DownloadUrl)
            .HasColumnName("download_url")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(e => e.FileSizeBytes)
            .HasColumnName("file_size_bytes");

        builder.Property(e => e.RecordCount)
            .HasColumnName("record_count");

        builder.Property(e => e.ExportedAt)
            .HasColumnName("exported_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        // One export object per format per month.
        builder.HasIndex(e => new { e.Year, e.Month, e.MediaType })
            .IsUnique()
            .HasDatabaseName("IX_measurement_exports_year_month_media_type");
    }
}
