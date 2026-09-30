using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HanaMedia.Migrations
{
    /// <inheritdoc />
    public partial class CampaignCompletionAndReceipt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_campaign_status",
                table: "campaigns");

            migrationBuilder.AddColumn<DateTime>(
                name: "AcceptedAt",
                table: "campaigns",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AcceptedByUserId",
                table: "campaigns",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "campaigns",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CompletedByUserId",
                table: "campaigns",
                type: "int",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "chk_campaign_status",
                table: "campaigns",
                sql: "[status] IN ('planning', 'running', 'paused', 'completed', 'accepted', 'cancelled')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM campaigns WHERE AcceptedAt IS NOT NULL OR CompletedAt IS NOT NULL OR status = 'accepted')
                    THROW 51000, 'Cannot remove campaign completion/receipt history. Back up and plan data recovery before rollback.', 1;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "chk_campaign_status",
                table: "campaigns");

            migrationBuilder.DropColumn(
                name: "AcceptedAt",
                table: "campaigns");

            migrationBuilder.DropColumn(
                name: "AcceptedByUserId",
                table: "campaigns");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "campaigns");

            migrationBuilder.DropColumn(
                name: "CompletedByUserId",
                table: "campaigns");

            migrationBuilder.AddCheckConstraint(
                name: "chk_campaign_status",
                table: "campaigns",
                sql: "[status] IN ('planning', 'running', 'paused', 'completed', 'cancelled')");
        }
    }
}
