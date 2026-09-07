using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriveSyncPing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FilesDeleted",
                table: "SyncJobs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FilesFailed",
                table: "SyncJobs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FilesIgnored",
                table: "SyncJobs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FilesUploaded",
                table: "SyncJobs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsDryRun",
                table: "SyncJobs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Trigger",
                table: "SyncJobs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncedAt",
                table: "SyncFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RemoteFileId",
                table: "SyncFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ScheduleConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    DaysOfWeek = table.Column<string>(type: "TEXT", nullable: false),
                    TimeOfDay = table.Column<string>(type: "TEXT", nullable: false),
                    LastRunUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleConfigs", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduleConfigs");

            migrationBuilder.DropColumn(
                name: "FilesDeleted",
                table: "SyncJobs");

            migrationBuilder.DropColumn(
                name: "FilesFailed",
                table: "SyncJobs");

            migrationBuilder.DropColumn(
                name: "FilesIgnored",
                table: "SyncJobs");

            migrationBuilder.DropColumn(
                name: "FilesUploaded",
                table: "SyncJobs");

            migrationBuilder.DropColumn(
                name: "IsDryRun",
                table: "SyncJobs");

            migrationBuilder.DropColumn(
                name: "Trigger",
                table: "SyncJobs");

            migrationBuilder.DropColumn(
                name: "LastSyncedAt",
                table: "SyncFiles");

            migrationBuilder.DropColumn(
                name: "RemoteFileId",
                table: "SyncFiles");
        }
    }
}
