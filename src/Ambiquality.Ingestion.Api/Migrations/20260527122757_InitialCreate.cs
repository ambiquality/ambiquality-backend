using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Ambiquality.Ingestion.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: init-databases.sql already enables it in production; this
            // makes the migration self-contained for fresh databases (e.g. tests).
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS timescaledb;");

            migrationBuilder.EnsureSchema(
                name: "ieq");

            migrationBuilder.CreateTable(
                name: "measurements",
                schema: "ieq",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    received_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    sensor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parameter_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    value = table.Column<double>(type: "double precision", nullable: false),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    observed_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    is_invalid = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    invalidated_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_measurements", x => new { x.id, x.received_at });
                });

            migrationBuilder.CreateTable(
                name: "parameter_ranges",
                schema: "ieq",
                columns: table => new
                {
                    parameter_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    min_value = table.Column<double>(type: "double precision", nullable: false),
                    max_value = table.Column<double>(type: "double precision", nullable: false),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parameter_ranges", x => x.parameter_code);
                });

            migrationBuilder.InsertData(
                schema: "ieq",
                table: "parameter_ranges",
                columns: new[] { "parameter_code", "max_value", "min_value", "unit" },
                values: new object[,]
                {
                    { "acoustics", 140.0, 0.0, "dB" },
                    { "co2", 50000.0, 0.0, "ppm" },
                    { "humidity", 100.0, 0.0, "%" },
                    { "light", 100000.0, 0.0, "lx" },
                    { "pm", 1000.0, 0.0, "µg/m³" },
                    { "temperature", 85.0, -40.0, "°C" },
                    { "voc", 60000.0, 0.0, "ppb" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_measurements_sensor_parameter_time",
                schema: "ieq",
                table: "measurements",
                columns: new[] { "sensor_id", "parameter_code", "received_at" });

            // Convert measurements into a TimescaleDB hypertable partitioned on the
            // received_at time column. Safe on an empty table; the PK (id, received_at)
            // already includes the partition column as TimescaleDB requires.
            migrationBuilder.Sql(
                "SELECT create_hypertable('ieq.measurements', by_range('received_at'));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "measurements",
                schema: "ieq");

            migrationBuilder.DropTable(
                name: "parameter_ranges",
                schema: "ieq");
        }
    }
}
