using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriveSyncPing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationRule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrganizationRule",
                table: "SyncFolders",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrganizationRule",
                table: "SyncFolders");
        }
    }
}
