using Ambiquality.Evidence.Api.Domain.Rooms;
using Ambiquality.Evidence.Api.Domain.Sensors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ambiquality.Evidence.Api.Infrastructure.Persistence;

public sealed class SensorConfiguration : IEntityTypeConfiguration<Sensor>
{
    public void Configure(EntityTypeBuilder<Sensor> builder)
    {
        builder.ToTable("sensors");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .ValueGeneratedNever();

        builder.Property(s => s.UriSlug)
            .HasColumnName("uri_slug")
            .HasMaxLength(255)
            .IsRequired();

        // A sensor has a stable, globally-unique slug independent of its room,
        // since it can be relocated.
        builder.HasIndex(s => s.UriSlug)
            .IsUnique()
            .HasDatabaseName("IX_sensor_uri_slug_unique");

        builder.Property(s => s.CurrentBuildingId)
            .HasColumnName("current_building_id")
            .IsRequired();

        builder.Property(s => s.CurrentRoomId)
            .HasColumnName("current_room_id")
            .IsRequired();

        builder.Property(s => s.CreatedBy)
            .HasColumnName("created_by")
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Foreign key to the room the sensor is currently placed in.
        builder.HasOne<Room>()
            .WithMany()
            .HasForeignKey(s => s.CurrentRoomId)
            .OnDelete(DeleteBehavior.Cascade);

        // Identity history (manufacturer, model, serial)
        builder.OwnsMany(s => s.IdentityHistory, ih =>
        {
            ih.ToTable("sensor_identity_history");
            ih.WithOwner().HasForeignKey(o => o.SensorId);
            ih.Property(o => o.SensorId).HasColumnName("sensor_id");
            ih.HasKey(o => new { o.SensorId, o.RecordedAt });

            ih.Property(i => i.Manufacturer)
                .HasColumnName("manufacturer")
                .HasMaxLength(255)
                .IsRequired();

            ih.Property(i => i.Model)
                .HasColumnName("model")
                .HasMaxLength(255)
                .IsRequired();

            ih.Property(i => i.SerialNumber)
                .HasColumnName("serial_number")
                .HasMaxLength(255)
                .IsRequired();

            ih.Property(i => i.Validity)
                .HasColumnName("validity")
                .HasColumnType("tstzrange")
                .IsRequired();

            ih.Property(i => i.RecordedAt)
                .HasColumnName("recorded_at")
                .IsRequired();

            ih.Property(i => i.RecordedBy)
                .HasColumnName("recorded_by")
                .IsRequired();
        });

        // Placement history (building + room over time)
        builder.OwnsMany(s => s.PlacementHistory, ph =>
        {
            ph.ToTable("sensor_placement_history");
            ph.WithOwner().HasForeignKey(o => o.SensorId);
            ph.Property(o => o.SensorId).HasColumnName("sensor_id");
            ph.HasKey(o => new { o.SensorId, o.RecordedAt });

            ph.Property(p => p.BuildingId)
                .HasColumnName("building_id")
                .IsRequired();

            ph.Property(p => p.RoomId)
                .HasColumnName("room_id")
                .IsRequired();

            ph.Property(p => p.Validity)
                .HasColumnName("validity")
                .HasColumnType("tstzrange")
                .IsRequired();

            ph.Property(p => p.RecordedAt)
                .HasColumnName("recorded_at")
                .IsRequired();

            ph.Property(p => p.RecordedBy)
                .HasColumnName("recorded_by")
                .IsRequired();
        });

        // Status history (lifecycle code from codelist)
        builder.OwnsMany(s => s.StatusHistory, sh =>
        {
            sh.ToTable("sensor_status_history");
            sh.WithOwner().HasForeignKey(o => o.SensorId);
            sh.Property(o => o.SensorId).HasColumnName("sensor_id");
            sh.HasKey(o => new { o.SensorId, o.RecordedAt });

            sh.Property(s2 => s2.StatusCode)
                .HasColumnName("status_code")
                .HasMaxLength(50)
                .IsRequired();

            sh.Property(s2 => s2.Validity)
                .HasColumnName("validity")
                .HasColumnType("tstzrange")
                .IsRequired();

            sh.Property(s2 => s2.RecordedAt)
                .HasColumnName("recorded_at")
                .IsRequired();

            sh.Property(s2 => s2.RecordedBy)
                .HasColumnName("recorded_by")
                .IsRequired();
        });

        // Measured parameter history (M:N with history — capabilities over time)
        builder.OwnsMany(s => s.MeasuredParameterHistory, mph =>
        {
            mph.ToTable("sensor_measured_parameter_history");
            mph.WithOwner().HasForeignKey(o => o.SensorId);
            mph.Property(o => o.SensorId).HasColumnName("sensor_id");
            mph.Property(m => m.ParameterCode)
                .HasColumnName("parameter_code")
                .HasMaxLength(50)
                .IsRequired();

            // parameter_code is part of the key: a sensor measures several
            // parameters whose history rows share the same recorded_at instant.
            mph.HasKey(o => new { o.SensorId, o.ParameterCode, o.RecordedAt });

            mph.Property(m => m.Validity)
                .HasColumnName("validity")
                .HasColumnType("tstzrange")
                .IsRequired();

            mph.Property(m => m.RecordedAt)
                .HasColumnName("recorded_at")
                .IsRequired();

            mph.Property(m => m.RecordedBy)
                .HasColumnName("recorded_by")
                .IsRequired();
        });
    }
}
