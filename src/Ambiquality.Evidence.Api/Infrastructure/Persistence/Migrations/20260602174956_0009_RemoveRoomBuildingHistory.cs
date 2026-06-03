using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Ambiquality.Evidence.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _0009_RemoveRoomBuildingHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "room_building_history",
                schema: "evidence");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "room_building_history",
                schema: "evidence",
                columns: table => new
                {
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    building_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    validity = table.Column<NpgsqlRange<DateTime>>(type: "tstzrange", nullable: false)
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
        }
    }
}
