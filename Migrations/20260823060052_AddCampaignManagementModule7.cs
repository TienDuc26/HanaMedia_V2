using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HanaMedia.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignManagementModule7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "campaign_id",
                table: "ideas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "campaign_id",
                table: "bookings",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "campaigns",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    client_name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    budget = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "draft"),
                    created_by_user_id = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaigns", x => x.id);
                    table.CheckConstraint("chk_campaign_budget", "[budget] >= 0");
                    table.CheckConstraint("chk_campaign_dates", "[end_date] >= [start_date]");
                    table.CheckConstraint("chk_campaign_status", "[status] IN ('draft', 'active', 'paused', 'completed', 'cancelled')");
                    table.ForeignKey(
                        name: "FK_campaigns_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ideas_campaign_id",
                table: "ideas",
                column: "campaign_id");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_campaign_id",
                table: "bookings",
                column: "campaign_id");

            migrationBuilder.CreateIndex(
                name: "IX_campaigns_created_by_user_id",
                table: "campaigns",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_campaigns_dates",
                table: "campaigns",
                columns: new[] { "start_date", "end_date" });

            migrationBuilder.CreateIndex(
                name: "IX_campaigns_status",
                table: "campaigns",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "UX_campaigns_client_name",
                table: "campaigns",
                columns: new[] { "client_name", "name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_bookings_campaigns_campaign_id",
                table: "bookings",
                column: "campaign_id",
                principalTable: "campaigns",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ideas_campaigns_campaign_id",
                table: "ideas",
                column: "campaign_id",
                principalTable: "campaigns",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_bookings_campaigns_campaign_id",
                table: "bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_ideas_campaigns_campaign_id",
                table: "ideas");

            migrationBuilder.DropTable(
                name: "campaigns");

            migrationBuilder.DropIndex(
                name: "IX_ideas_campaign_id",
                table: "ideas");

            migrationBuilder.DropIndex(
                name: "IX_bookings_campaign_id",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "campaign_id",
                table: "ideas");

            migrationBuilder.DropColumn(
                name: "campaign_id",
                table: "bookings");
        }
    }
}
