using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ambiquality.Evidence.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixCompositeKeys : Migration
    {
        // This migration contains no schema changes.
        // InitialCreate already creates the history tables with the correct schema
        // using raw SQL (composite PKs on (building_id, validity_lower) and GiST
        // exclusion constraints). This migration exists solely to align the EF model
        // snapshot with the actual configuration: composite key (building_id, RecordedAt)
        // used by EF Core for change-tracking (INSERT/UPDATE), while the DB enforces
        // uniqueness via the validity_lower-based PK and GiST constraints.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
