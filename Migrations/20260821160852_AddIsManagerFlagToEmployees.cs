using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HanaMedia.Migrations
{
    /// <inheritdoc />
    public partial class AddIsManagerFlagToEmployees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_manager",
                table: "employees",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_manager",
                table: "employees");
        }
    }
}
