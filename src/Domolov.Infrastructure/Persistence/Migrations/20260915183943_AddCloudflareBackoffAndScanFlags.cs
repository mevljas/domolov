using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domolov.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCloudflareBackoffAndScanFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CloudflareBlockedUntil",
                table: "Watches",
                type: "timestamp with time zone",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "CloudflareStrikeCount",
                table: "Watches",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<bool>(
                name: "CloudflareBlocked",
                table: "ScanRuns",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CloudflareBlockedUntil", table: "Watches");

            migrationBuilder.DropColumn(name: "CloudflareStrikeCount", table: "Watches");

            migrationBuilder.DropColumn(name: "CloudflareBlocked", table: "ScanRuns");
        }
    }
}
