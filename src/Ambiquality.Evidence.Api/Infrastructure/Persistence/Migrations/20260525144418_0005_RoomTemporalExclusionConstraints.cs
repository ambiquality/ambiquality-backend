using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ambiquality.Evidence.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _0005_RoomTemporalExclusionConstraints : Migration
    {
        private static readonly string[] SingleValueHistoryTables =
        {
            "room_name_history",
            "room_floor_history",
            "room_building_history",
            "room_function_history",
            "room_exposure_history",
            "room_geometry_history",
            "room_ventilation_history",
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // -----------------------------------------------------------------
            // Bring the room history tables in line with buildings and sensors.
            //
            // 1. Repair legacy rows. Closed rows used to be written with the raw
            //    two-argument NpgsqlRange ctor, which produces an *inclusive*
            //    upper bound [lower, validFrom]. That boundary instant is shared
            //    with the next open row [validFrom, +inf), so the two rows
            //    overlap — which would make step 2 fail. Rewrite any finite,
            //    upper-inclusive range to the half-open [lower, validFrom) form.
            //    Open rows (infinite upper) are left untouched.
            //
            // 2. Add GiST exclusion constraints enforcing the temporal
            //    no-overlap invariant at the database level. Single-value
            //    attributes forbid two rows for the same room whose validity
            //    ranges overlap; the pollution-source collection is scoped per
            //    source_code, since a room can have several sources at once.
            //
            //    The constraints are DEFERRABLE INITIALLY DEFERRED: a change
            //    closes the open row (UPDATE) and opens a new one (INSERT) in a
            //    single transaction, and EF may emit the INSERT first. Deferring
            //    the check to COMMIT lets both rows settle before it runs.
            // -----------------------------------------------------------------
            foreach (var table in SingleValueHistoryTables)
            {
                RepairInclusiveUpperBounds(migrationBuilder, table);
            }

            RepairInclusiveUpperBounds(migrationBuilder, "room_pollution_source_history");

            foreach (var table in SingleValueHistoryTables)
            {
                migrationBuilder.Sql(
                    $"ALTER TABLE evidence.{table} " +
                    $"ADD CONSTRAINT {table}_no_overlapping_validity " +
                    "EXCLUDE USING gist (room_id WITH =, validity WITH &&) " +
                    "DEFERRABLE INITIALLY DEFERRED;");
            }

            migrationBuilder.Sql(
                "ALTER TABLE evidence.room_pollution_source_history " +
                "ADD CONSTRAINT room_pollution_source_history_no_overlapping_validity " +
                "EXCLUDE USING gist (room_id WITH =, source_code WITH =, validity WITH &&) " +
                "DEFERRABLE INITIALLY DEFERRED;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only the constraints are reversible; the half-open repair is a
            // correctness fix and is intentionally not undone.
            migrationBuilder.Sql(
                "ALTER TABLE evidence.room_pollution_source_history " +
                "DROP CONSTRAINT room_pollution_source_history_no_overlapping_validity;");

            foreach (var table in SingleValueHistoryTables)
            {
                migrationBuilder.Sql(
                    $"ALTER TABLE evidence.{table} " +
                    $"DROP CONSTRAINT {table}_no_overlapping_validity;");
            }
        }

        private static void RepairInclusiveUpperBounds(MigrationBuilder migrationBuilder, string table)
        {
            migrationBuilder.Sql(
                $"UPDATE evidence.{table} " +
                "SET validity = tstzrange(lower(validity), upper(validity), '[)') " +
                "WHERE NOT lower_inf(validity) AND NOT upper_inf(validity) AND upper_inc(validity);");
        }
    }
}
