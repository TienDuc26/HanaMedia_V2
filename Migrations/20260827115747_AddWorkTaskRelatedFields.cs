using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HanaMedia.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkTaskRelatedFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RelatedId",
                table: "work_tasks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RelatedType",
                table: "work_tasks",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RelatedId",
                table: "work_tasks");

            migrationBuilder.DropColumn(
                name: "RelatedType",
                table: "work_tasks");
        }
    }
}
