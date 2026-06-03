using Ambiquality.Evidence.Api.Domain.Buildings;
using Ambiquality.Evidence.Api.Domain.Rooms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ambiquality.Evidence.Api.Infrastructure.Persistence;

public sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("rooms");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .ValueGeneratedNever();

        builder.Property(r => r.BuildingId)
            .HasColumnName("building_id")
            .IsRequired();

        builder.Property(r => r.UriSlug)
            .HasColumnName("uri_slug")
            .HasMaxLength(255)
            .IsRequired();

        // Slugs are server-generated and globally unique (like sensors), so a
        // room slug never collides regardless of building.
        builder.HasIndex(r => r.UriSlug)
            .IsUnique()
            .HasDatabaseName("IX_room_uri_slug_unique");

        builder.Property(r => r.CreatedBy)
            .HasColumnName("created_by")
            .IsRequired();

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Foreign key to buildings
        builder.HasOne<Building>()
            .WithMany()
            .HasForeignKey(r => r.BuildingId)
            .OnDelete(DeleteBehavior.Cascade);

        // Name history
        builder.OwnsMany(r => r.NameHistory, nh =>
        {
            nh.ToTable("room_name_history");
            nh.WithOwner().HasForeignKey(o => o.RoomId);
            nh.Property(o => o.RoomId).HasColumnName("room_id");
            nh.HasKey(o => new { o.RoomId, o.RecordedAt });

            nh.Property(n => n.Name)
                .HasColumnName("name")
                .HasMaxLength(255)
                .IsRequired();

            nh.Property(n => n.Validity)
                .HasColumnName("validity")
                .HasColumnType("tstzrange")
                .IsRequired();

            nh.Property(n => n.RecordedAt)
                .HasColumnName("recorded_at")
                .IsRequired();

            nh.Property(n => n.RecordedBy)
                .HasColumnName("recorded_by")
                .IsRequired();
        });

        // Floor history
        builder.OwnsMany(r => r.FloorHistory, fh =>
        {
            fh.ToTable("room_floor_history");
            fh.WithOwner().HasForeignKey(o => o.RoomId);
            fh.Property(o => o.RoomId).HasColumnName("room_id");
            fh.HasKey(o => new { o.RoomId, o.RecordedAt });

            fh.Property(f => f.Floor)
                .HasColumnName("floor")
                .IsRequired();

            fh.Property(f => f.Validity)
                .HasColumnName("validity")
                .HasColumnType("tstzrange")
                .IsRequired();

            fh.Property(f => f.RecordedAt)
                .HasColumnName("recorded_at")
                .IsRequired();

            fh.Property(f => f.RecordedBy)
                .HasColumnName("recorded_by")
                .IsRequired();
        });

        // Function history (room function code from codelist)
        builder.OwnsMany(r => r.FunctionHistory, fnh =>
        {
            fnh.ToTable("room_function_history");
            fnh.WithOwner().HasForeignKey(o => o.RoomId);
            fnh.Property(o => o.RoomId).HasColumnName("room_id");
            fnh.HasKey(o => new { o.RoomId, o.RecordedAt });

            fnh.Property(f => f.FunctionCode)
                .HasColumnName("function_code")
                .HasMaxLength(50);

            fnh.Property(f => f.Validity)
                .HasColumnName("validity")
                .HasColumnType("tstzrange")
                .IsRequired();

            fnh.Property(f => f.RecordedAt)
                .HasColumnName("recorded_at")
                .IsRequired();

            fnh.Property(f => f.RecordedBy)
                .HasColumnName("recorded_by")
                .IsRequired();
        });

        // Exposure history (exposure code from codelist)
        builder.OwnsMany(r => r.ExposureHistory, eh =>
        {
            eh.ToTable("room_exposure_history");
            eh.WithOwner().HasForeignKey(o => o.RoomId);
            eh.Property(o => o.RoomId).HasColumnName("room_id");
            eh.HasKey(o => new { o.RoomId, o.RecordedAt });

            eh.Property(e => e.ExposureCode)
                .HasColumnName("exposure_code")
                .HasMaxLength(50);

            eh.Property(e => e.Validity)
                .HasColumnName("validity")
                .HasColumnType("tstzrange")
                .IsRequired();

            eh.Property(e => e.RecordedAt)
                .HasColumnName("recorded_at")
                .IsRequired();

            eh.Property(e => e.RecordedBy)
                .HasColumnName("recorded_by")
                .IsRequired();
        });

        // Geometry history (floor area, ceiling height)
        builder.OwnsMany(r => r.GeometryHistory, gh =>
        {
            gh.ToTable("room_geometry_history");
            gh.WithOwner().HasForeignKey(o => o.RoomId);
            gh.Property(o => o.RoomId).HasColumnName("room_id");
            gh.HasKey(o => new { o.RoomId, o.RecordedAt });

            gh.Property(g => g.AreaM2)
                .HasColumnName("area_m2")
                .HasPrecision(10, 2);

            gh.Property(g => g.CeilingHeightM)
                .HasColumnName("ceiling_height_m")
                .HasPrecision(5, 2);

            gh.Property(g => g.Validity)
                .HasColumnName("validity")
                .HasColumnType("tstzrange")
                .IsRequired();

            gh.Property(g => g.RecordedAt)
                .HasColumnName("recorded_at")
                .IsRequired();

            gh.Property(g => g.RecordedBy)
                .HasColumnName("recorded_by")
                .IsRequired();
        });

        // Ventilation history (ventilation type code from codelist)
        builder.OwnsMany(r => r.VentilationHistory, vh =>
        {
            vh.ToTable("room_ventilation_history");
            vh.WithOwner().HasForeignKey(o => o.RoomId);
            vh.Property(o => o.RoomId).HasColumnName("room_id");
            vh.HasKey(o => new { o.RoomId, o.RecordedAt });

            vh.Property(v => v.VentilationType)
                .HasColumnName("ventilation_type")
                .HasMaxLength(50);

            vh.Property(v => v.Validity)
                .HasColumnName("validity")
                .HasColumnType("tstzrange")
                .IsRequired();

            vh.Property(v => v.RecordedAt)
                .HasColumnName("recorded_at")
                .IsRequired();

            vh.Property(v => v.RecordedBy)
                .HasColumnName("recorded_by")
                .IsRequired();
        });

        // Pollution source history (M:N with history - allows tracking source changes over time)
        builder.OwnsMany(r => r.PollutionSourceHistory, psh =>
        {
            psh.ToTable("room_pollution_source_history");
            psh.WithOwner().HasForeignKey(o => o.RoomId);
            psh.Property(o => o.RoomId).HasColumnName("room_id");
            psh.HasKey(o => new { o.RoomId, o.RecordedAt });

            psh.Property(p => p.SourceCode)
                .HasColumnName("source_code")
                .HasMaxLength(100)
                .IsRequired();

            psh.Property(p => p.Validity)
                .HasColumnName("validity")
                .HasColumnType("tstzrange")
                .IsRequired();

            psh.Property(p => p.RecordedAt)
                .HasColumnName("recorded_at")
                .IsRequired();

            psh.Property(p => p.RecordedBy)
                .HasColumnName("recorded_by")
                .IsRequired();
        });
    }
}
