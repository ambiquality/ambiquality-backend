using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ambiquality.Evidence.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _0008_GlobalRoomSlug : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Room slugs are now server-generated and globally unique (was per-building).
            // The composite (building_id, uri_slug) index is replaced by a global unique
            // index on uri_slug plus a plain building_id index for lookups. NOTE: creating
            // the global unique index fails if any existing rooms share a slug across
            // buildings — recreate the dev DB (./dev.sh down && up) if so.
            migrationBuilder.DropIndex(
                name: "IX_room_building_uri_slug_unique",
                schema: "evidence",
                table: "rooms");

            migrationBuilder.CreateIndex(
                name: "IX_room_uri_slug_unique",
                schema: "evidence",
                table: "rooms",
                column: "uri_slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rooms_building_id",
                schema: "evidence",
                table: "rooms",
                column: "building_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_room_uri_slug_unique",
                schema: "evidence",
                table: "rooms");

            migrationBuilder.DropIndex(
                name: "IX_rooms_building_id",
                schema: "evidence",
                table: "rooms");

            migrationBuilder.CreateIndex(
                name: "IX_room_building_uri_slug_unique",
                schema: "evidence",
                table: "rooms",
                columns: new[] { "building_id", "uri_slug" },
                unique: true);
        }
    }
}
