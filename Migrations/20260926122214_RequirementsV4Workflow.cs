using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HanaMedia.Migrations
{
    /// <inheritdoc />
    public partial class RequirementsV4Workflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PrimaryKolId",
                table: "ideas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmedAt",
                table: "campaigns",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConfirmedByUserId",
                table: "campaigns",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcceptanceFileUrl",
                table: "bookings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CastPercent",
                table: "bookings",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CommissionPercent",
                table: "bookings",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CompanyPercent",
                table: "bookings",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ContractRevision",
                table: "bookings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FinanceVersion",
                table: "bookings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LegalApprovedRevision",
                table: "bookings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalFeedback",
                table: "bookings",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LegalReviewedAt",
                table: "bookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LegalReviewedByUserId",
                table: "bookings",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "booking_kols",
                columns: table => new
                {
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    KolId = table.Column<int>(type: "int", nullable: false),
                    CastAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_kols", x => new { x.BookingId, x.KolId });
                    table.ForeignKey(
                        name: "FK_booking_kols_bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_booking_kols_kols_KolId",
                        column: x => x.KolId,
                        principalTable: "kols",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "booking_payments",
                columns: table => new
                {
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PayeeId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsPaid = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_payments", x => new { x.BookingId, x.Kind, x.PayeeId });
                    table.ForeignKey(
                        name: "FK_booking_payments_bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ideas_PrimaryKolId",
                table: "ideas",
                column: "PrimaryKolId");

            migrationBuilder.CreateIndex(
                name: "IX_booking_kols_KolId",
                table: "booking_kols",
                column: "KolId");

            migrationBuilder.AddForeignKey(
                name: "FK_ideas_kols_PrimaryKolId",
                table: "ideas",
                column: "PrimaryKolId",
                principalTable: "kols",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
            // Preserve historical amounts; only migrate relationship and unfinished workflow.
            migrationBuilder.Sql(@"INSERT INTO booking_kols (BookingId, KolId, CastAmount)
                SELECT id, kol_id, 0 FROM bookings WHERE kol_id IS NOT NULL;
                UPDATE bookings SET contract_status = 'cho_phap_ly', ContractRevision = 1
                WHERE contract_status = 'cho_ky' AND contract_file_url IS NOT NULL;
                UPDATE bookings SET contract_status = 'da_duyet'
                WHERE contract_status = 'cho_ky' AND contract_file_url IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ideas_kols_PrimaryKolId",
                table: "ideas");

            migrationBuilder.DropTable(
                name: "booking_kols");

            migrationBuilder.DropTable(
                name: "booking_payments");

            migrationBuilder.DropIndex(
                name: "IX_ideas_PrimaryKolId",
                table: "ideas");

            migrationBuilder.DropColumn(
                name: "PrimaryKolId",
                table: "ideas");

            migrationBuilder.DropColumn(
                name: "ConfirmedAt",
                table: "campaigns");

            migrationBuilder.DropColumn(
                name: "ConfirmedByUserId",
                table: "campaigns");

            migrationBuilder.DropColumn(
                name: "AcceptanceFileUrl",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "CastPercent",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "CommissionPercent",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "CompanyPercent",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "ContractRevision",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "FinanceVersion",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "LegalApprovedRevision",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "LegalFeedback",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "LegalReviewedAt",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "LegalReviewedByUserId",
                table: "bookings");
        }
    }
}
