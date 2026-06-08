using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ambiquality.Evidence.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _0010_OfnCzechAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "anonymization",
                schema: "evidence",
                table: "building_location_history");

            migrationBuilder.DropColumn(
                name: "country",
                schema: "evidence",
                table: "building_address_history");

            migrationBuilder.DropColumn(
                name: "postcode",
                schema: "evidence",
                table: "building_address_history");

            migrationBuilder.DropColumn(
                name: "street",
                schema: "evidence",
                table: "building_address_history");

            migrationBuilder.RenameColumn(
                name: "city",
                schema: "evidence",
                table: "building_address_history",
                newName: "municipality_name");

            migrationBuilder.AddColumn<long>(
                name: "address_point_code",
                schema: "evidence",
                table: "building_address_history",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "district_name",
                schema: "evidence",
                table: "building_address_history",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "house_number",
                schema: "evidence",
                table: "building_address_history",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "house_number_type",
                schema: "evidence",
                table: "building_address_history",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "municipality_part_name",
                schema: "evidence",
                table: "building_address_history",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "orientation_number",
                schema: "evidence",
                table: "building_address_history",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "orientation_number_letter",
                schema: "evidence",
                table: "building_address_history",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "psc",
                schema: "evidence",
                table: "building_address_history",
                type: "character varying(5)",
                maxLength: 5,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "region_name",
                schema: "evidence",
                table: "building_address_history",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "street_name",
                schema: "evidence",
                table: "building_address_history",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "address_point_code",
                schema: "evidence",
                table: "building_address_history");

            migrationBuilder.DropColumn(
                name: "district_name",
                schema: "evidence",
                table: "building_address_history");

            migrationBuilder.DropColumn(
                name: "house_number",
                schema: "evidence",
                table: "building_address_history");

            migrationBuilder.DropColumn(
                name: "house_number_type",
                schema: "evidence",
                table: "building_address_history");

            migrationBuilder.DropColumn(
                name: "municipality_part_name",
                schema: "evidence",
                table: "building_address_history");

            migrationBuilder.DropColumn(
                name: "orientation_number",
                schema: "evidence",
                table: "building_address_history");

            migrationBuilder.DropColumn(
                name: "orientation_number_letter",
                schema: "evidence",
                table: "building_address_history");

            migrationBuilder.DropColumn(
                name: "psc",
                schema: "evidence",
                table: "building_address_history");

            migrationBuilder.DropColumn(
                name: "region_name",
                schema: "evidence",
                table: "building_address_history");

            migrationBuilder.DropColumn(
                name: "street_name",
                schema: "evidence",
                table: "building_address_history");

            migrationBuilder.RenameColumn(
                name: "municipality_name",
                schema: "evidence",
                table: "building_address_history",
                newName: "city");

            migrationBuilder.AddColumn<string>(
                name: "anonymization",
                schema: "evidence",
                table: "building_location_history",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "country",
                schema: "evidence",
                table: "building_address_history",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "postcode",
                schema: "evidence",
                table: "building_address_history",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "street",
                schema: "evidence",
                table: "building_address_history",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");
        }
    }
}
