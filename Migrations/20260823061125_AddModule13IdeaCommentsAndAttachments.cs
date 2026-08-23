using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HanaMedia.Migrations
{
    /// <inheritdoc />
    public partial class AddModule13IdeaCommentsAndAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "idea_id",
                table: "work_tasks",
                type: "int",
                nullable: true);

            // Mở rộng ràng buộc CHECK constraint cho status của ý tưởng — bổ sung 'in_production'
            // (bước "Triển khai" trong quy trình 6 bước của Module 13).
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'chk_idea_status')
                    ALTER TABLE [ideas] DROP CONSTRAINT [chk_idea_status];
                ALTER TABLE [ideas] ADD CONSTRAINT [chk_idea_status]
                    CHECK ([status] IN ('y_tuong', 'review', 'need_revision', 'approved', 'in_production', 'done'));
            ");

            migrationBuilder.AddColumn<string>(
                name: "moodboard_file_url",
                table: "ideas",
                type: "varchar(255)",
                unicode: false,
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reference_file_url",
                table: "ideas",
                type: "varchar(255)",
                unicode: false,
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "idea_comments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    idea_id = table.Column<int>(type: "int", nullable: false),
                    author_user_id = table.Column<int>(type: "int", nullable: true),
                    comment_type = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false, defaultValue: "general"),
                    content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idea_comments", x => x.id);
                    table.ForeignKey(
                        name: "FK_idea_comments_ideas_idea_id",
                        column: x => x.idea_id,
                        principalTable: "ideas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_idea_comments_users_author_user_id",
                        column: x => x.author_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_work_tasks_idea_id",
                table: "work_tasks",
                column: "idea_id");

            migrationBuilder.CreateIndex(
                name: "idx_idea_comments_idea_created",
                table: "idea_comments",
                columns: new[] { "idea_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_idea_comments_author_user_id",
                table: "idea_comments",
                column: "author_user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_work_tasks_ideas_idea_id",
                table: "work_tasks",
                column: "idea_id",
                principalTable: "ideas",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_work_tasks_ideas_idea_id",
                table: "work_tasks");

            migrationBuilder.DropTable(
                name: "idea_comments");

            migrationBuilder.DropIndex(
                name: "IX_work_tasks_idea_id",
                table: "work_tasks");

            migrationBuilder.DropColumn(
                name: "idea_id",
                table: "work_tasks");

            migrationBuilder.DropColumn(
                name: "moodboard_file_url",
                table: "ideas");

            migrationBuilder.DropColumn(
                name: "reference_file_url",
                table: "ideas");

            // Khôi phục constraint ban đầu (5 giá trị, không có 'in_production').
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'chk_idea_status')
                    ALTER TABLE [ideas] DROP CONSTRAINT [chk_idea_status];
                ALTER TABLE [ideas] ADD CONSTRAINT [chk_idea_status]
                    CHECK ([status] IN ('y_tuong', 'review', 'need_revision', 'approved', 'done'));
            ");
        }
    }
}
