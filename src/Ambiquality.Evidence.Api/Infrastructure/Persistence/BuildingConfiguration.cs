using Ambiquality.Evidence.Api.Domain.Buildings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ambiquality.Evidence.Api.Infrastructure.Persistence;

public sealed class BuildingConfiguration : IEntityTypeConfiguration<Building>
{
    public void Configure(EntityTypeBuilder<Building> builder)
    {
        builder.ToTable("buildings");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
            .ValueGeneratedNever();

        builder.Property(b => b.UriSlug)
            .HasColumnName("uri_slug")
            .HasMaxLength(255)
            .IsRequired();

        builder.HasIndex(b => b.UriSlug).IsUnique();

        builder.Property(b => b.OwnerId)
            .HasColumnName("owner_id")
            .IsRequired();

        builder.Property(b => b.CreatedBy)
            .HasColumnName("created_by")
            .IsRequired();

        builder.Property(b => b.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Name history
        builder.OwnsMany(b => b.NameHistory, nh =>
        {
            nh.ToTable("building_name_history");
            nh.WithOwner().HasForeignKey("building_id");
            nh.HasKey("building_id", "RecordedAt");

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

        // Address history
        builder.OwnsMany(b => b.AddressHistory, ah =>
        {
            ah.ToTable("building_address_history");
            ah.WithOwner().HasForeignKey("building_id");
            ah.HasKey("building_id", "RecordedAt");

            // OFN "Adresy" (2020-07-01) structured address, anchored on the RÚIAN
            // address-point code. Street name, orientation number, municipal part,
            // district (okres) and region (kraj) are optional per the standard.
            ah.OwnsOne(a => a.Address, aco =>
            {
                aco.Property(a => a.AddressPointCode)
                    .HasColumnName("address_point_code")
                    .IsRequired();

                aco.Property(a => a.StreetName)
                    .HasColumnName("street_name")
                    .HasMaxLength(255);

                aco.Property(a => a.HouseNumber)
                    .HasColumnName("house_number")
                    .IsRequired();

                aco.Property(a => a.HouseNumberType)
                    .HasColumnName("house_number_type")
                    .HasMaxLength(8)
                    .IsRequired();

                aco.Property(a => a.OrientationNumber)
                    .HasColumnName("orientation_number");

                aco.Property(a => a.OrientationNumberLetter)
                    .HasColumnName("orientation_number_letter")
                    .HasMaxLength(2);

                aco.Property(a => a.MunicipalityName)
                    .HasColumnName("municipality_name")
                    .HasMaxLength(100)
                    .IsRequired();

                aco.Property(a => a.MunicipalityPartName)
                    .HasColumnName("municipality_part_name")
                    .HasMaxLength(100);

                aco.Property(a => a.Psc)
                    .HasColumnName("psc")
                    .HasMaxLength(5)
                    .IsRequired();

                aco.Property(a => a.DistrictName)
                    .HasColumnName("district_name")
                    .HasMaxLength(100);

                aco.Property(a => a.RegionName)
                    .HasColumnName("region_name")
                    .HasMaxLength(100);
            });

            ah.Property(a => a.Validity)
                .HasColumnName("validity")
                .HasColumnType("tstzrange")
                .IsRequired();

            ah.Property(a => a.RecordedAt)
                .HasColumnName("recorded_at")
                .IsRequired();

            ah.Property(a => a.RecordedBy)
                .HasColumnName("recorded_by")
                .IsRequired();
        });

        // Type history
        builder.OwnsMany(b => b.TypeHistory, th =>
        {
            th.ToTable("building_type_history");
            th.WithOwner().HasForeignKey("building_id");
            th.HasKey("building_id", "RecordedAt");

            th.Property(t => t.BuildingTypeCode)
                .HasColumnName("building_type_code")
                .HasMaxLength(50)
                .IsRequired();

            th.Property(t => t.Validity)
                .HasColumnName("validity")
                .HasColumnType("tstzrange")
                .IsRequired();

            th.Property(t => t.RecordedAt)
                .HasColumnName("recorded_at")
                .IsRequired();

            th.Property(t => t.RecordedBy)
                .HasColumnName("recorded_by")
                .IsRequired();
        });

        // Location history
        builder.OwnsMany(b => b.LocationHistory, lh =>
        {
            lh.ToTable("building_location_history");
            lh.WithOwner().HasForeignKey("building_id");
            lh.HasKey("building_id", "RecordedAt");

            lh.OwnsOne(l => l.Coordinates, co =>
            {
                co.Property(c => c.Latitude)
                    .HasColumnName("latitude")
                    .HasPrecision(9, 6);

                co.Property(c => c.Longitude)
                    .HasColumnName("longitude")
                    .HasPrecision(9, 6);
            });

            lh.Property(l => l.Validity)
                .HasColumnName("validity")
                .HasColumnType("tstzrange")
                .IsRequired();

            lh.Property(l => l.RecordedAt)
                .HasColumnName("recorded_at")
                .IsRequired();

            lh.Property(l => l.RecordedBy)
                .HasColumnName("recorded_by")
                .IsRequired();
        });

        // Years history
        builder.OwnsMany(b => b.YearsHistory, yh =>
        {
            yh.ToTable("building_years_history");
            yh.WithOwner().HasForeignKey("building_id");
            yh.HasKey("building_id", "RecordedAt");

            yh.Property(y => y.YearBuilt)
                .HasColumnName("year_built");

            yh.Property(y => y.YearRenovated)
                .HasColumnName("year_renovated");

            yh.Property(y => y.Validity)
                .HasColumnName("validity")
                .HasColumnType("tstzrange")
                .IsRequired();

            yh.Property(y => y.RecordedAt)
                .HasColumnName("recorded_at")
                .IsRequired();

            yh.Property(y => y.RecordedBy)
                .HasColumnName("recorded_by")
                .IsRequired();
        });
    }
}
