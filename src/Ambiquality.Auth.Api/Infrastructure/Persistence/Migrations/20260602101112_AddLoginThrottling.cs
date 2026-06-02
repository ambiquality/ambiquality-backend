using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ambiquality.Auth.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLoginThrottling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "failed_login_count",
                schema: "auth",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_failed_login_at",
                schema: "auth",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "failed_login_count",
                schema: "auth",
                table: "users");

            migrationBuilder.DropColumn(
                name: "last_failed_login_at",
                schema: "auth",
                table: "users");
        }
    }
}
