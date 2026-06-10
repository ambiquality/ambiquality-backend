using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Ambiquality.Evidence.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _0012_SensorInstallation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sensor_installation_history",
                schema: "evidence",
                columns: table => new
                {
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    sensor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position_note = table.Column<string>(type: "text", nullable: true),
                    distance_window_m = table.Column<double>(type: "double precision", nullable: true),
                    distance_door_m = table.Column<double>(type: "double precision", nullable: true),
                    distance_source_m = table.Column<double>(type: "double precision", nullable: true),
                    measurement_frequency_seconds = table.Column<int>(type: "integer", nullable: true),
                    installed_on = table.Column<DateOnly>(type: "date", nullable: true),
                    last_calibrated_on = table.Column<DateOnly>(type: "date", nullable: true),
                    validity = table.Column<NpgsqlRange<DateTime>>(type: "tstzrange", nullable: false),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensor_installation_history", x => new { x.sensor_id, x.recorded_at });
                    table.ForeignKey(
                        name: "FK_sensor_installation_history_sensors_sensor_id",
                        column: x => x.sensor_id,
                        principalSchema: "evidence",
                        principalTable: "sensors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // -----------------------------------------------------------------
            // GiST exclusion constraint — same single-value temporal no-overlap
            // guarantee the identity / placement / status history tables get
            // (see 0004_SensorAggregate). Two installation rows for the same
            // sensor may never overlap in time. DEFERRABLE INITIALLY DEFERRED so
            // a close (UPDATE) + open (INSERT) emitted in one transaction — in
            // either order by EF — is only checked at COMMIT, once both rows have
            // settled.
            // -----------------------------------------------------------------
            migrationBuilder.Sql(
                "ALTER TABLE evidence.sensor_installation_history " +
                "ADD CONSTRAINT sensor_installation_history_no_overlapping_validity " +
                "EXCLUDE USING gist (sensor_id WITH =, validity WITH &&) " +
                "DEFERRABLE INITIALLY DEFERRED;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sensor_installation_history",
                schema: "evidence");
        }
    }
}
