using HanaMedia.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HanaMedia.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260910090000_AddIdeaLibraryModule14Indexes")]
public sealed class AddIdeaLibraryModule14Indexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "idx_ideas_industry",
            table: "ideas",
            column: "industry");

        migrationBuilder.CreateIndex(
            name: "idx_ideas_client",
            table: "ideas",
            column: "client_name");

        migrationBuilder.CreateIndex(
            name: "idx_ideas_category",
            table: "ideas",
            column: "category");

        migrationBuilder.CreateIndex(
            name: "idx_ideas_category_status",
            table: "ideas",
            columns: new[] { "category", "status" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "idx_ideas_industry", table: "ideas");
        migrationBuilder.DropIndex(name: "idx_ideas_client", table: "ideas");
        migrationBuilder.DropIndex(name: "idx_ideas_category", table: "ideas");
        migrationBuilder.DropIndex(name: "idx_ideas_category_status", table: "ideas");
    }
}
