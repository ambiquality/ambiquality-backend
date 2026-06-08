using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ambiquality.Evidence.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _0011_OfnRuianCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "district_code",
                schema: "evidence",
                table: "building_address_history",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "municipality_code",
                schema: "evidence",
                table: "building_address_history",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "municipality_part_code",
                schema: "evidence",
                table: "building_address_history",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "region_code",
                schema: "evidence",
                table: "building_address_history",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "street_code",
                schema: "evidence",
                table: "building_address_history",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "district_code",
                schema: "evidence",
                table: "building_address_history");

            migrationBuilder.DropColumn(
                name: "municipality_code",
                schema: "evidence",
                table: "building_address_history");

            migrationBuilder.DropColumn(
                name: "municipality_part_code",
                schema: "evidence",
                table: "building_address_history");

            migrationBuilder.DropColumn(
                name: "region_code",
                schema: "evidence",
                table: "building_address_history");

            migrationBuilder.DropColumn(
                name: "street_code",
                schema: "evidence",
                table: "building_address_history");
        }
    }
}
