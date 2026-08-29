using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HanaMedia.Migrations
{
    /// <inheritdoc />
    public partial class UpdateKolsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_kol_platform",
                table: "kols");

            migrationBuilder.AlterColumn<string>(
                name: "platform",
                table: "kols",
                type: "varchar(200)",
                unicode: false,
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldUnicode: false,
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "niche",
                table: "kols",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "kols",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_active",
                table: "kols");

            migrationBuilder.AlterColumn<string>(
                name: "platform",
                table: "kols",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(200)",
                oldUnicode: false,
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "niche",
                table: "kols",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255);

            migrationBuilder.AddCheckConstraint(
                name: "chk_kol_platform",
                table: "kols",
                sql: "[platform] IN ('TikTok', 'Instagram', 'YouTube', 'Facebook')");
        }
    }
}
