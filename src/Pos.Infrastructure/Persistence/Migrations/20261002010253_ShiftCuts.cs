using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ShiftCuts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShiftCuts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 1, nullable: false),
                    Number = table.Column<long>(type: "INTEGER", nullable: false),
                    ShiftId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ShiftNumber = table.Column<long>(type: "INTEGER", nullable: false),
                    RegisterCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ShiftOpenedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    ShiftOpenedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    GeneratedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    AuthorizedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    OpeningFloatCents = table.Column<long>(type: "INTEGER", nullable: false),
                    SalesCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CancelledCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalSoldCents = table.Column<long>(type: "INTEGER", nullable: false),
                    CashSalesCents = table.Column<long>(type: "INTEGER", nullable: false),
                    CashCancelledCents = table.Column<long>(type: "INTEGER", nullable: false),
                    CardCents = table.Column<long>(type: "INTEGER", nullable: false),
                    TransferCents = table.Column<long>(type: "INTEGER", nullable: false),
                    CashRefundsCents = table.Column<long>(type: "INTEGER", nullable: false),
                    NonCashRefundsCents = table.Column<long>(type: "INTEGER", nullable: false),
                    CreditNotesIssuedCents = table.Column<long>(type: "INTEGER", nullable: false),
                    OnAccountSalesCents = table.Column<long>(type: "INTEGER", nullable: false),
                    CustomerPaymentsCashCents = table.Column<long>(type: "INTEGER", nullable: false),
                    CustomerPaymentsNonCashCents = table.Column<long>(type: "INTEGER", nullable: false),
                    CustomerPaymentVoidsCashCents = table.Column<long>(type: "INTEGER", nullable: false),
                    CustomerPaymentVoidsNonCashCents = table.Column<long>(type: "INTEGER", nullable: false),
                    DepositsCents = table.Column<long>(type: "INTEGER", nullable: false),
                    WithdrawalsCents = table.Column<long>(type: "INTEGER", nullable: false),
                    ExpectedCashCents = table.Column<long>(type: "INTEGER", nullable: false),
                    CountedCashCents = table.Column<long>(type: "INTEGER", nullable: true),
                    DifferenceCents = table.Column<long>(type: "INTEGER", nullable: true),
                    Comment = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftCuts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftCuts_CashShifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "CashShifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftCuts_ClosingPerShift",
                table: "ShiftCuts",
                column: "ShiftId",
                unique: true,
                filter: "\"Type\" = 'Z'");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftCuts_GeneratedAt",
                table: "ShiftCuts",
                columns: new[] { "GeneratedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftCuts_GeneratedBy_GeneratedAt",
                table: "ShiftCuts",
                columns: new[] { "GeneratedBy", "GeneratedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftCuts_ShiftId",
                table: "ShiftCuts",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftCuts_Type_Number",
                table: "ShiftCuts",
                columns: new[] { "Type", "Number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShiftCuts");
        }
    }
}
