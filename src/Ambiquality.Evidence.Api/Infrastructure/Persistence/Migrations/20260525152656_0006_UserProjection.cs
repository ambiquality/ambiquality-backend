using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ambiquality.Evidence.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _0006_UserProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_projections",
                schema: "evidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    auth_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_projections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_projections_auth_user_id_unique",
                schema: "evidence",
                table: "user_projections",
                column: "auth_user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_projections",
                schema: "evidence");
        }
    }
}
