using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WidenReportAttachmentColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fix "value too long for type character varying(50)" - Excel MIME type is 62 chars,
            // file paths can be long. Alter to TEXT to support any length.
            migrationBuilder.Sql(@"
                ALTER TABLE daily_report_attachments
                    ALTER COLUMN attachment_path TYPE TEXT;
                ALTER TABLE daily_report_attachments
                    ALTER COLUMN file_type TYPE TEXT;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE daily_report_attachments
                    ALTER COLUMN attachment_path TYPE VARCHAR(50) USING LEFT(attachment_path, 50);
                ALTER TABLE daily_report_attachments
                    ALTER COLUMN file_type TYPE VARCHAR(50) USING LEFT(file_type, 50);
            ");
        }
    }
}
