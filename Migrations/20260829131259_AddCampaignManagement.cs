using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HanaMedia.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "campaign_id",
                table: "work_tasks",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "campaigns",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    client = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    budget = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    manager_employee_id = table.Column<int>(type: "int", nullable: false),
                    status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false, defaultValue: "planning"),
                    notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaigns", x => x.id);
                    table.CheckConstraint("chk_campaign_status", "[status] IN ('planning', 'running', 'paused', 'completed', 'cancelled')");
                    table.ForeignKey(
                        name: "FK_campaigns_employees_manager_employee_id",
                        column: x => x.manager_employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_work_tasks_campaign_id",
                table: "work_tasks",
                column: "campaign_id");

            migrationBuilder.CreateIndex(
                name: "IX_campaigns_manager_employee_id",
                table: "campaigns",
                column: "manager_employee_id");

            migrationBuilder.AddForeignKey(
                name: "FK_work_tasks_campaigns_campaign_id",
                table: "work_tasks",
                column: "campaign_id",
                principalTable: "campaigns",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_work_tasks_campaigns_campaign_id",
                table: "work_tasks");

            migrationBuilder.DropTable(
                name: "campaigns");

            migrationBuilder.DropIndex(
                name: "IX_work_tasks_campaign_id",
                table: "work_tasks");

            migrationBuilder.DropColumn(
                name: "campaign_id",
                table: "work_tasks");
        }
    }
}
