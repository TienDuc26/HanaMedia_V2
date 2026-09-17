using HanaMedia.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HanaMedia.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260911090000_AddDirectorIdeaReviewModule15")]
public sealed class AddDirectorIdeaReviewModule15 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "director_review_status",
            table: "ideas",
            type: "varchar(30)",
            unicode: false,
            maxLength: 30,
            nullable: false,
            defaultValue: "pending");

        migrationBuilder.AddColumn<string>(
            name: "director_feedback",
            table: "ideas",
            type: "nvarchar(2000)",
            maxLength: 2000,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "director_reviewed_by_user_id",
            table: "ideas",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "director_reviewed_at",
            table: "ideas",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "chk_idea_director_review_status",
            table: "ideas",
            sql: "[director_review_status] IN ('pending', 'revision_requested', 'approved', 'rejected')");

        migrationBuilder.CreateIndex(
            name: "idx_ideas_director_review_status",
            table: "ideas",
            column: "director_review_status");

        migrationBuilder.CreateIndex(
            name: "IX_ideas_director_reviewed_by_user_id",
            table: "ideas",
            column: "director_reviewed_by_user_id");

        migrationBuilder.AddForeignKey(
            name: "FK_ideas_users_director_reviewed_by_user_id",
            table: "ideas",
            column: "director_reviewed_by_user_id",
            principalTable: "users",
            principalColumn: "id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_ideas_users_director_reviewed_by_user_id",
            table: "ideas");

        migrationBuilder.DropCheckConstraint(
            name: "chk_idea_director_review_status",
            table: "ideas");

        migrationBuilder.DropIndex(
            name: "idx_ideas_director_review_status",
            table: "ideas");

        migrationBuilder.DropIndex(
            name: "IX_ideas_director_reviewed_by_user_id",
            table: "ideas");

        migrationBuilder.DropColumn(name: "director_feedback", table: "ideas");
        migrationBuilder.DropColumn(name: "director_review_status", table: "ideas");
        migrationBuilder.DropColumn(name: "director_reviewed_at", table: "ideas");
        migrationBuilder.DropColumn(name: "director_reviewed_by_user_id", table: "ideas");
    }
}
