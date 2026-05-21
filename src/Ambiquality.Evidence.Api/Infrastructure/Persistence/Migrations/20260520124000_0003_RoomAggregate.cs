using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Ambiquality.Evidence.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _0003_RoomAggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rooms",
                schema: "evidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    uri_slug = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    building_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rooms_buildings_building_id",
                        column: x => x.building_id,
                        principalSchema: "evidence",
                        principalTable: "buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "room_building_history",
                schema: "evidence",
                columns: table => new
                {
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    validity = table.Column<NpgsqlRange<DateTime>>(type: "tstzrange", nullable: false),
                    building_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_room_building_history", x => new { x.room_id, x.recorded_at });
                    table.ForeignKey(
                        name: "FK_room_building_history_rooms_room_id",
                        column: x => x.room_id,
                        principalSchema: "evidence",
                        principalTable: "rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "room_exposure_history",
                schema: "evidence",
                columns: table => new
                {
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    validity = table.Column<NpgsqlRange<DateTime>>(type: "tstzrange", nullable: false),
                    exposure_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_room_exposure_history", x => new { x.room_id, x.recorded_at });
                    table.ForeignKey(
                        name: "FK_room_exposure_history_rooms_room_id",
                        column: x => x.room_id,
                        principalSchema: "evidence",
                        principalTable: "rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "room_floor_history",
                schema: "evidence",
                columns: table => new
                {
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    validity = table.Column<NpgsqlRange<DateTime>>(type: "tstzrange", nullable: false),
                    floor = table.Column<byte>(type: "smallint", nullable: false),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_room_floor_history", x => new { x.room_id, x.recorded_at });
                    table.ForeignKey(
                        name: "FK_room_floor_history_rooms_room_id",
                        column: x => x.room_id,
                        principalSchema: "evidence",
                        principalTable: "rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "room_function_history",
                schema: "evidence",
                columns: table => new
                {
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    validity = table.Column<NpgsqlRange<DateTime>>(type: "tstzrange", nullable: false),
                    function_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_room_function_history", x => new { x.room_id, x.recorded_at });
                    table.ForeignKey(
                        name: "FK_room_function_history_rooms_room_id",
                        column: x => x.room_id,
                        principalSchema: "evidence",
                        principalTable: "rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "room_geometry_history",
                schema: "evidence",
                columns: table => new
                {
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    validity = table.Column<NpgsqlRange<DateTime>>(type: "tstzrange", nullable: false),
                    area_m2 = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    ceiling_height_m = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_room_geometry_history", x => new { x.room_id, x.recorded_at });
                    table.ForeignKey(
                        name: "FK_room_geometry_history_rooms_room_id",
                        column: x => x.room_id,
                        principalSchema: "evidence",
                        principalTable: "rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "room_name_history",
                schema: "evidence",
                columns: table => new
                {
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    validity = table.Column<NpgsqlRange<DateTime>>(type: "tstzrange", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_room_name_history", x => new { x.room_id, x.recorded_at });
                    table.ForeignKey(
                        name: "FK_room_name_history_rooms_room_id",
                        column: x => x.room_id,
                        principalSchema: "evidence",
                        principalTable: "rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "room_pollution_source_history",
                schema: "evidence",
                columns: table => new
                {
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    validity = table.Column<NpgsqlRange<DateTime>>(type: "tstzrange", nullable: false),
                    source_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_room_pollution_source_history", x => new { x.room_id, x.recorded_at });
                    table.ForeignKey(
                        name: "FK_room_pollution_source_history_rooms_room_id",
                        column: x => x.room_id,
                        principalSchema: "evidence",
                        principalTable: "rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "room_ventilation_history",
                schema: "evidence",
                columns: table => new
                {
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    validity = table.Column<NpgsqlRange<DateTime>>(type: "tstzrange", nullable: false),
                    ventilation_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_room_ventilation_history", x => new { x.room_id, x.recorded_at });
                    table.ForeignKey(
                        name: "FK_room_ventilation_history_rooms_room_id",
                        column: x => x.room_id,
                        principalSchema: "evidence",
                        principalTable: "rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_room_building_uri_slug_unique",
                schema: "evidence",
                table: "rooms",
                columns: new[] { "building_id", "uri_slug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "room_ventilation_history",
                schema: "evidence");

            migrationBuilder.DropTable(
                name: "room_pollution_source_history",
                schema: "evidence");

            migrationBuilder.DropTable(
                name: "room_name_history",
                schema: "evidence");

            migrationBuilder.DropTable(
                name: "room_geometry_history",
                schema: "evidence");

            migrationBuilder.DropTable(
                name: "room_function_history",
                schema: "evidence");

            migrationBuilder.DropTable(
                name: "room_floor_history",
                schema: "evidence");

            migrationBuilder.DropTable(
                name: "room_exposure_history",
                schema: "evidence");

            migrationBuilder.DropTable(
                name: "room_building_history",
                schema: "evidence");

            migrationBuilder.DropTable(
                name: "rooms",
                schema: "evidence");
        }
    }
}
