using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixHighValueThresholdType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE users
                ALTER COLUMN high_value_threshold TYPE NUMERIC USING high_value_threshold::NUMERIC;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE users
                ALTER COLUMN high_value_threshold TYPE real USING high_value_threshold::real;
            ");
        }
    }
}
