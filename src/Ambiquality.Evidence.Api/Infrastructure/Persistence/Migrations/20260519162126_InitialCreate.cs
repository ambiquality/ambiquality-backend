using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Ambiquality.Evidence.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "evidence");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,");

            migrationBuilder.CreateTable(
                name: "buildings",
                schema: "evidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    uri_slug = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_buildings", x => x.Id);
                });

            // -----------------------------------------------------------------
            // History tables — created without the EF-synthesised integer
            // surrogate key. Each table gets a STORED generated column
            // `validity_lower = lower(validity)` and a composite primary key
            // `(building_id, validity_lower)`. A GiST exclusion constraint
            // enforces non-overlapping validity ranges per building, and a
            // supporting btree index on `(building_id, validity)` accelerates
            // as-of queries (locked plan §7, §5).
            // -----------------------------------------------------------------

            migrationBuilder.CreateTable(
                name: "building_address_history",
                schema: "evidence",
                columns: table => new
                {
                    building_id = table.Column<Guid>(type: "uuid", nullable: false),
                    street = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    postcode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    validity = table.Column<NpgsqlRange<DateTime>>(type: "tstzrange", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.ForeignKey(
                        name: "FK_building_address_history_buildings_building_id",
                        column: x => x.building_id,
                        principalSchema: "evidence",
                        principalTable: "buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "building_location_history",
                schema: "evidence",
                columns: table => new
                {
                    building_id = table.Column<Guid>(type: "uuid", nullable: false),
                    latitude = table.Column<double>(type: "double precision", precision: 9, scale: 6, nullable: true),
                    longitude = table.Column<double>(type: "double precision", precision: 9, scale: 6, nullable: true),
                    anonymization = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    validity = table.Column<NpgsqlRange<DateTime>>(type: "tstzrange", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.ForeignKey(
                        name: "FK_building_location_history_buildings_building_id",
                        column: x => x.building_id,
                        principalSchema: "evidence",
                        principalTable: "buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "building_name_history",
                schema: "evidence",
                columns: table => new
                {
                    building_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    validity = table.Column<NpgsqlRange<DateTime>>(type: "tstzrange", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.ForeignKey(
                        name: "FK_building_name_history_buildings_building_id",
                        column: x => x.building_id,
                        principalSchema: "evidence",
                        principalTable: "buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "building_type_history",
                schema: "evidence",
                columns: table => new
                {
                    building_id = table.Column<Guid>(type: "uuid", nullable: false),
                    building_type_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    validity = table.Column<NpgsqlRange<DateTime>>(type: "tstzrange", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.ForeignKey(
                        name: "FK_building_type_history_buildings_building_id",
                        column: x => x.building_id,
                        principalSchema: "evidence",
                        principalTable: "buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "building_years_history",
                schema: "evidence",
                columns: table => new
                {
                    building_id = table.Column<Guid>(type: "uuid", nullable: false),
                    year_built = table.Column<short>(type: "smallint", nullable: true),
                    year_renovated = table.Column<short>(type: "smallint", nullable: true),
                    validity = table.Column<NpgsqlRange<DateTime>>(type: "tstzrange", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.ForeignKey(
                        name: "FK_building_years_history_buildings_building_id",
                        column: x => x.building_id,
                        principalSchema: "evidence",
                        principalTable: "buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_buildings_uri_slug",
                schema: "evidence",
                table: "buildings",
                column: "uri_slug",
                unique: true);

            // -----------------------------------------------------------------
            // Composite primary key + GiST exclusion + as-of index per history
            // table. The `validity_lower` STORED generated column gives us a
            // stable, indexable scalar derived from `lower(validity)` so that
            // the composite PK is deterministic without requiring a surrogate.
            // -----------------------------------------------------------------

            string[] historyTables =
            {
                "building_name_history",
                "building_address_history",
                "building_type_history",
                "building_location_history",
                "building_years_history",
            };

            foreach (var table in historyTables)
            {
                migrationBuilder.Sql(
                    $"ALTER TABLE evidence.{table} " +
                    "ADD COLUMN validity_lower timestamp with time zone " +
                    "GENERATED ALWAYS AS (lower(validity)) STORED;");

                migrationBuilder.Sql(
                    $"ALTER TABLE evidence.{table} " +
                    $"ADD CONSTRAINT pk_{table} PRIMARY KEY (building_id, validity_lower);");

                migrationBuilder.Sql(
                    $"ALTER TABLE evidence.{table} " +
                    $"ADD CONSTRAINT excl_{table}_overlapping " +
                    "EXCLUDE USING gist (building_id WITH =, validity WITH &&);");

                migrationBuilder.Sql(
                    $"CREATE INDEX idx_{table}_building_id_validity " +
                    $"ON evidence.{table} (building_id, validity);");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            string[] historyTables =
            {
                "building_name_history",
                "building_address_history",
                "building_type_history",
                "building_location_history",
                "building_years_history",
            };

            foreach (var table in historyTables)
            {
                migrationBuilder.Sql($"DROP INDEX IF EXISTS evidence.idx_{table}_building_id_validity;");
                migrationBuilder.Sql($"ALTER TABLE evidence.{table} DROP CONSTRAINT IF EXISTS excl_{table}_overlapping;");
                migrationBuilder.Sql($"ALTER TABLE evidence.{table} DROP CONSTRAINT IF EXISTS pk_{table};");
                migrationBuilder.Sql($"ALTER TABLE evidence.{table} DROP COLUMN IF EXISTS validity_lower;");
            }

            migrationBuilder.DropTable(
                name: "building_address_history",
                schema: "evidence");

            migrationBuilder.DropTable(
                name: "building_location_history",
                schema: "evidence");

            migrationBuilder.DropTable(
                name: "building_name_history",
                schema: "evidence");

            migrationBuilder.DropTable(
                name: "building_type_history",
                schema: "evidence");

            migrationBuilder.DropTable(
                name: "building_years_history",
                schema: "evidence");

            migrationBuilder.DropTable(
                name: "buildings",
                schema: "evidence");
        }
    }
}
