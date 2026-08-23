using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HanaMedia.Migrations
{
    /// <inheritdoc />
    public partial class CompleteKolKocManagementModule8 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "kols",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "UX_kols_platform_profile_link",
                table: "kols",
                columns: new[] { "platform", "profile_link" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "chk_kol_booking_price",
                table: "kols",
                sql: "[booking_price] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "chk_kol_engagement",
                table: "kols",
                sql: "[engagement_rate] BETWEEN 0 AND 100");

            migrationBuilder.AddCheckConstraint(
                name: "chk_kol_followers",
                table: "kols",
                sql: "[followers_count] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_kols_platform_profile_link",
                table: "kols");

            migrationBuilder.DropCheckConstraint(
                name: "chk_kol_booking_price",
                table: "kols");

            migrationBuilder.DropCheckConstraint(
                name: "chk_kol_engagement",
                table: "kols");

            migrationBuilder.DropCheckConstraint(
                name: "chk_kol_followers",
                table: "kols");

            migrationBuilder.DropColumn(
                name: "row_version",
                table: "kols");
        }
    }
}
