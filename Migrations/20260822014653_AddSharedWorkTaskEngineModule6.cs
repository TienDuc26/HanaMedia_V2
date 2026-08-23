using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HanaMedia.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedWorkTaskEngineModule6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "work_tasks",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    module = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    assigned_employee_id = table.Column<int>(type: "int", nullable: false),
                    created_by_user_id = table.Column<int>(type: "int", nullable: false),
                    reviewer_user_id = table.Column<int>(type: "int", nullable: false),
                    deadline = table.Column<DateTime>(type: "datetime2", nullable: false),
                    status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false, defaultValue: "todo"),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    completed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_tasks", x => x.id);
                    table.CheckConstraint("chk_work_task_module", "[module] IN ('Nhan_Su', 'Booking', 'Y_Tuong')");
                    table.CheckConstraint("chk_work_task_status", "[status] IN ('todo', 'in_progress', 'review', 'need_revision', 'approved', 'done')");
                    table.ForeignKey(
                        name: "FK_work_tasks_employees_assigned_employee_id",
                        column: x => x.assigned_employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_work_tasks_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_work_tasks_users_reviewer_user_id",
                        column: x => x.reviewer_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "work_task_history",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    work_task_id = table.Column<int>(type: "int", nullable: false),
                    actor_user_id = table.Column<int>(type: "int", nullable: true),
                    from_status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    to_status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_task_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_work_task_history_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_work_task_history_work_tasks_work_task_id",
                        column: x => x.work_task_id,
                        principalTable: "work_tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_work_task_history_task_created",
                table: "work_task_history",
                columns: new[] { "work_task_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_work_task_history_actor_user_id",
                table: "work_task_history",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_work_tasks_assignee_status",
                table: "work_tasks",
                columns: new[] { "assigned_employee_id", "status" });

            migrationBuilder.CreateIndex(
                name: "idx_work_tasks_module_deadline",
                table: "work_tasks",
                columns: new[] { "module", "deadline" });

            migrationBuilder.CreateIndex(
                name: "IX_work_tasks_created_by_user_id",
                table: "work_tasks",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_tasks_reviewer_user_id",
                table: "work_tasks",
                column: "reviewer_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "work_task_history");

            migrationBuilder.DropTable(
                name: "work_tasks");
        }
    }
}
