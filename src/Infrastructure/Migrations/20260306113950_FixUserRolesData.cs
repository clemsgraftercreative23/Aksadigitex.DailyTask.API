using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixUserRolesData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fix user role IDs to match UserRole enum values
            // User 427: roleId 4 (invalid) → 3 (SuperDuperAdmin)
            migrationBuilder.Sql(@"UPDATE users SET role_id = 3 WHERE id = 427;");
            
            // User 430: roleId 1 (AdminDivisi) → 0 (User)
            migrationBuilder.Sql(@"UPDATE users SET role_id = 0 WHERE id = 430;");

            // Ensure roles table has correct entries
            migrationBuilder.Sql(@"
                INSERT INTO roles (id, role_name) VALUES
                  (0, 'User'),
                  (1, 'AdminDivisi'),
                  (2, 'SuperAdmin'),
                  (3, 'SuperDuperAdmin')
                ON CONFLICT (id) DO UPDATE SET role_name = EXCLUDED.role_name;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert role IDs to original values on rollback
            migrationBuilder.Sql(@"UPDATE users SET role_id = 4 WHERE id = 427;");
            migrationBuilder.Sql(@"UPDATE users SET role_id = 1 WHERE id = 430;");
        }
    }
}
