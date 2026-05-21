using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Ambiquality.Evidence.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _0004_SensorAggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sensors",
                schema: "evidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    uri_slug = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    current_building_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sensors_rooms_current_room_id",
                        column: x => x.current_room_id,
                        principalSchema: "evidence",
                        principalTable: "rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sensor_identity_history",
                schema: "evidence",
                columns: table => new
                {
                    sensor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    validity = table.Column<NpgsqlRange<DateTime>>(type: "tstzrange", nullable: false),
                    manufacturer = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    model = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    serial_number = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensor_identity_history", x => new { x.sensor_id, x.recorded_at });
                    table.ForeignKey(
                        name: "FK_sensor_identity_history_sensors_sensor_id",
                        column: x => x.sensor_id,
                        principalSchema: "evidence",
                        principalTable: "sensors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sensor_measured_parameter_history",
                schema: "evidence",
                columns: table => new
                {
                    sensor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parameter_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    validity = table.Column<NpgsqlRange<DateTime>>(type: "tstzrange", nullable: false),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensor_measured_parameter_history", x => new { x.sensor_id, x.parameter_code, x.recorded_at });
                    table.ForeignKey(
                        name: "FK_sensor_measured_parameter_history_sensors_sensor_id",
                        column: x => x.sensor_id,
                        principalSchema: "evidence",
                        principalTable: "sensors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sensor_placement_history",
                schema: "evidence",
                columns: table => new
                {
                    sensor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    validity = table.Column<NpgsqlRange<DateTime>>(type: "tstzrange", nullable: false),
                    building_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensor_placement_history", x => new { x.sensor_id, x.recorded_at });
                    table.ForeignKey(
                        name: "FK_sensor_placement_history_sensors_sensor_id",
                        column: x => x.sensor_id,
                        principalSchema: "evidence",
                        principalTable: "sensors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sensor_status_history",
                schema: "evidence",
                columns: table => new
                {
                    sensor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    validity = table.Column<NpgsqlRange<DateTime>>(type: "tstzrange", nullable: false),
                    status_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensor_status_history", x => new { x.sensor_id, x.recorded_at });
                    table.ForeignKey(
                        name: "FK_sensor_status_history_sensors_sensor_id",
                        column: x => x.sensor_id,
                        principalSchema: "evidence",
                        principalTable: "sensors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sensor_uri_slug_unique",
                schema: "evidence",
                table: "sensors",
                column: "uri_slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sensors_current_room_id",
                schema: "evidence",
                table: "sensors",
                column: "current_room_id");

            // -----------------------------------------------------------------
            // GiST exclusion constraints enforce the temporal no-overlap
            // invariant at the database level (the same guarantee the building
            // history tables get). Single-value attributes forbid two rows for
            // the same sensor whose validity ranges overlap; the measured-
            // parameter collection is scoped per parameter_code, since a sensor
            // legitimately measures several parameters at the same time.
            //
            // The constraints are DEFERRABLE INITIALLY DEFERRED: changing an
            // attribute closes the open row (UPDATE) and opens a new one
            // (INSERT) in a single transaction, and EF may emit the INSERT
            // before the UPDATE. Deferring the check to COMMIT lets both rows
            // settle (the closed row's upper bound is set first by then), so a
            // legitimate close+open does not transiently look like an overlap.
            // -----------------------------------------------------------------
            string[] singleValueHistoryTables =
            {
                "sensor_identity_history",
                "sensor_placement_history",
                "sensor_status_history",
            };

            foreach (var table in singleValueHistoryTables)
            {
                migrationBuilder.Sql(
                    $"ALTER TABLE evidence.{table} " +
                    $"ADD CONSTRAINT {table}_no_overlapping_validity " +
                    "EXCLUDE USING gist (sensor_id WITH =, validity WITH &&) " +
                    "DEFERRABLE INITIALLY DEFERRED;");
            }

            migrationBuilder.Sql(
                "ALTER TABLE evidence.sensor_measured_parameter_history " +
                "ADD CONSTRAINT sensor_measured_parameter_history_no_overlapping_validity " +
                "EXCLUDE USING gist (sensor_id WITH =, parameter_code WITH =, validity WITH &&) " +
                "DEFERRABLE INITIALLY DEFERRED;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sensor_identity_history",
                schema: "evidence");

            migrationBuilder.DropTable(
                name: "sensor_measured_parameter_history",
                schema: "evidence");

            migrationBuilder.DropTable(
                name: "sensor_placement_history",
                schema: "evidence");

            migrationBuilder.DropTable(
                name: "sensor_status_history",
                schema: "evidence");

            migrationBuilder.DropTable(
                name: "sensors",
                schema: "evidence");
        }
    }
}
