using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CashShifts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CashShiftId",
                table: "Sales",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CashShifts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Number = table.Column<long>(type: "INTEGER", nullable: false),
                    RegisterCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    OpenedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OpeningFloatCents = table.Column<long>(type: "INTEGER", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ClosedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    SalesCount = table.Column<int>(type: "INTEGER", nullable: true),
                    CancelledCount = table.Column<int>(type: "INTEGER", nullable: true),
                    TotalSoldCents = table.Column<long>(type: "INTEGER", nullable: true),
                    CashSalesCents = table.Column<long>(type: "INTEGER", nullable: true),
                    CashCancelledCents = table.Column<long>(type: "INTEGER", nullable: true),
                    CardCents = table.Column<long>(type: "INTEGER", nullable: true),
                    TransferCents = table.Column<long>(type: "INTEGER", nullable: true),
                    DepositsCents = table.Column<long>(type: "INTEGER", nullable: true),
                    WithdrawalsCents = table.Column<long>(type: "INTEGER", nullable: true),
                    ExpectedCashCents = table.Column<long>(type: "INTEGER", nullable: true),
                    CountedCashCents = table.Column<long>(type: "INTEGER", nullable: true),
                    DifferenceCents = table.Column<long>(type: "INTEGER", nullable: true),
                    ClosingComment = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashShifts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CashMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CashShiftId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    AmountCents = table.Column<long>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                    AuthorizedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashMovements_CashShifts_CashShiftId",
                        column: x => x.CashShiftId,
                        principalTable: "CashShifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sales_CashShiftId_Status",
                table: "Sales",
                columns: new[] { "CashShiftId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_Shift_Sequence",
                table: "CashMovements",
                columns: new[] { "CashShiftId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashShifts_Number",
                table: "CashShifts",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashShifts_OpenedAt",
                table: "CashShifts",
                columns: new[] { "OpenedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CashShifts_OpenedBy_OpenedAt",
                table: "CashShifts",
                columns: new[] { "OpenedBy", "OpenedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CashShifts_OpenPerRegister",
                table: "CashShifts",
                column: "RegisterCode",
                unique: true,
                filter: "\"Status\" = 'OPEN'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashMovements");

            migrationBuilder.DropTable(
                name: "CashShifts");

            migrationBuilder.DropIndex(
                name: "IX_Sales_CashShiftId_Status",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CashShiftId",
                table: "Sales");
        }
    }
}
