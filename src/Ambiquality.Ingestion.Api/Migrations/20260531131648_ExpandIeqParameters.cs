using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Ambiquality.Ingestion.Api.Migrations
{
    /// <inheritdoc />
    public partial class ExpandIeqParameters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "ieq",
                table: "parameter_ranges",
                keyColumn: "parameter_code",
                keyValue: "acoustics");

            migrationBuilder.DeleteData(
                schema: "ieq",
                table: "parameter_ranges",
                keyColumn: "parameter_code",
                keyValue: "light");

            migrationBuilder.DeleteData(
                schema: "ieq",
                table: "parameter_ranges",
                keyColumn: "parameter_code",
                keyValue: "pm");

            migrationBuilder.InsertData(
                schema: "ieq",
                table: "parameter_ranges",
                columns: new[] { "parameter_code", "max_value", "min_value", "unit" },
                values: new object[,]
                {
                    { "air_velocity", 10.0, 0.0, "m/s" },
                    { "cct", 20000.0, 1000.0, "K" },
                    { "co", 2000.0, 0.0, "ppm" },
                    { "eco2", 65000.0, 0.0, "ppm" },
                    { "illuminance", 100000.0, 0.0, "lx" },
                    { "laeq", 140.0, 0.0, "dB(A)" },
                    { "no2", 500.0, 0.0, "µg/m³" },
                    { "o3", 500.0, 0.0, "µg/m³" },
                    { "pm1", 500.0, 0.0, "µg/m³" },
                    { "pm10", 1000.0, 0.0, "µg/m³" },
                    { "pm2_5", 500.0, 0.0, "µg/m³" },
                    { "pm4", 1000.0, 0.0, "µg/m³" },
                    { "pressure", 110000.0, 85000.0, "Pa" },
                    { "so2", 500.0, 0.0, "µg/m³" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "ieq",
                table: "parameter_ranges",
                keyColumn: "parameter_code",
                keyValue: "air_velocity");

            migrationBuilder.DeleteData(
                schema: "ieq",
                table: "parameter_ranges",
                keyColumn: "parameter_code",
                keyValue: "cct");

            migrationBuilder.DeleteData(
                schema: "ieq",
                table: "parameter_ranges",
                keyColumn: "parameter_code",
                keyValue: "co");

            migrationBuilder.DeleteData(
                schema: "ieq",
                table: "parameter_ranges",
                keyColumn: "parameter_code",
                keyValue: "eco2");

            migrationBuilder.DeleteData(
                schema: "ieq",
                table: "parameter_ranges",
                keyColumn: "parameter_code",
                keyValue: "illuminance");

            migrationBuilder.DeleteData(
                schema: "ieq",
                table: "parameter_ranges",
                keyColumn: "parameter_code",
                keyValue: "laeq");

            migrationBuilder.DeleteData(
                schema: "ieq",
                table: "parameter_ranges",
                keyColumn: "parameter_code",
                keyValue: "no2");

            migrationBuilder.DeleteData(
                schema: "ieq",
                table: "parameter_ranges",
                keyColumn: "parameter_code",
                keyValue: "o3");

            migrationBuilder.DeleteData(
                schema: "ieq",
                table: "parameter_ranges",
                keyColumn: "parameter_code",
                keyValue: "pm1");

            migrationBuilder.DeleteData(
                schema: "ieq",
                table: "parameter_ranges",
                keyColumn: "parameter_code",
                keyValue: "pm10");

            migrationBuilder.DeleteData(
                schema: "ieq",
                table: "parameter_ranges",
                keyColumn: "parameter_code",
                keyValue: "pm2_5");

            migrationBuilder.DeleteData(
                schema: "ieq",
                table: "parameter_ranges",
                keyColumn: "parameter_code",
                keyValue: "pm4");

            migrationBuilder.DeleteData(
                schema: "ieq",
                table: "parameter_ranges",
                keyColumn: "parameter_code",
                keyValue: "pressure");

            migrationBuilder.DeleteData(
                schema: "ieq",
                table: "parameter_ranges",
                keyColumn: "parameter_code",
                keyValue: "so2");

            migrationBuilder.InsertData(
                schema: "ieq",
                table: "parameter_ranges",
                columns: new[] { "parameter_code", "max_value", "min_value", "unit" },
                values: new object[,]
                {
                    { "acoustics", 140.0, 0.0, "dB" },
                    { "light", 100000.0, 0.0, "lx" },
                    { "pm", 1000.0, 0.0, "µg/m³" }
                });
        }
    }
}
