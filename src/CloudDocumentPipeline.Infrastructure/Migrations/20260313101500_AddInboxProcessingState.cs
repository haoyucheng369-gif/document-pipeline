using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudDocumentPipeline.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInboxProcessingState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ClaimedAtUtc",
                table: "InboxMessages",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(2026, 3, 13, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "InboxMessages",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "InboxMessages",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Processed");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ProcessedAtUtc",
                table: "InboxMessages",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.Sql("""
                UPDATE InboxMessages
                SET ClaimedAtUtc = ISNULL(ProcessedAtUtc, SYSUTCDATETIME()),
                    Status = 'Processed'
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClaimedAtUtc",
                table: "InboxMessages");

            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "InboxMessages");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "InboxMessages");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ProcessedAtUtc",
                table: "InboxMessages",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);
        }
    }
}
