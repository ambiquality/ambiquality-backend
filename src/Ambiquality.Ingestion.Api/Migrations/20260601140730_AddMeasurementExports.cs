using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ambiquality.Ingestion.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMeasurementExports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "measurement_exports",
                schema: "ieq",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    year = table.Column<short>(type: "smallint", nullable: false),
                    month = table.Column<short>(type: "smallint", nullable: false),
                    media_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    compress_format = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    file_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    download_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    record_count = table.Column<long>(type: "bigint", nullable: true),
                    exported_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_measurement_exports", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_measurement_exports_year_month_media_type",
                schema: "ieq",
                table: "measurement_exports",
                columns: new[] { "year", "month", "media_type" },
                unique: true);

            // Export.Worker writes only this table (the measurements hypertable stays
            // append-only via the ingestion path). The export_worker login is created
            // at cluster init; guard so test containers without that role still migrate.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'export_worker') THEN
                        GRANT SELECT, INSERT ON ieq.measurement_exports TO export_worker;
                    END IF;
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "measurement_exports",
                schema: "ieq");
        }
    }
}
