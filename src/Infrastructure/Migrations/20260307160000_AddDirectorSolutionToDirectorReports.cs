using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectorSolutionToDirectorReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE director_reports ADD COLUMN IF NOT EXISTS director_solution TEXT;
                ALTER TABLE director_reports ADD COLUMN IF NOT EXISTS is_asked_director BOOLEAN DEFAULT FALSE;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE director_reports DROP COLUMN IF EXISTS director_solution;
                ALTER TABLE director_reports DROP COLUMN IF EXISTS is_asked_director;
            ");
        }
    }
}
