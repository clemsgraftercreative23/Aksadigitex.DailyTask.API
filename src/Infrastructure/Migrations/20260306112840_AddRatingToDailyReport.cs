using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRatingToDailyReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rating column already exists from initial schema migration
            // This is a no-op migration to keep the migration history in sync with the model
            migrationBuilder.Sql(@"
                -- Ensure rating column exists (it should already exist)
                ALTER TABLE daily_report ADD COLUMN IF NOT EXISTS rating INTEGER;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Don't drop the column on down, as it's part of the schema
        }
    }
}
