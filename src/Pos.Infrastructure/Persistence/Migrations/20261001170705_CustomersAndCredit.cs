using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomersAndCredit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CustomerPaymentVoidsCashCents",
                table: "CashShifts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CustomerPaymentVoidsNonCashCents",
                table: "CashShifts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CustomerPaymentsCashCents",
                table: "CashShifts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CustomerPaymentsNonCashCents",
                table: "CashShifts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "OnAccountSalesCents",
                table: "CashShifts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 254, nullable: true),
                    TaxId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    CreditLimitCents = table.Column<long>(type: "INTEGER", nullable: false),
                    CreditMode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    SearchText = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Number = table.Column<long>(type: "INTEGER", nullable: false),
                    RequestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CustomerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AmountCents = table.Column<long>(type: "INTEGER", nullable: false),
                    Method = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Reference = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    CashShiftId = table.Column<Guid>(type: "TEXT", nullable: true),
                    BalanceBeforeCents = table.Column<long>(type: "INTEGER", nullable: false),
                    BalanceAfterCents = table.Column<long>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    VoidedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    VoidedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    VoidAuthorizedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    VoidReason = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    VoidCashShiftId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerPayments_CashShifts_CashShiftId",
                        column: x => x.CashShiftId,
                        principalTable: "CashShifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerPayments_CashShifts_VoidCashShiftId",
                        column: x => x.VoidCashShiftId,
                        principalTable: "CashShifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerPayments_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Receivables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SaleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CustomerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CustomerName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    OriginalCents = table.Column<long>(type: "INTEGER", nullable: false),
                    BalanceCents = table.Column<long>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    OverLimitAuthorizedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Receivables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Receivables_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Receivables_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReceivableEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReceivableId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 14, nullable: false),
                    AmountCents = table.Column<long>(type: "INTEGER", nullable: false),
                    CustomerPaymentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SaleReturnId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceivableEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceivableEntries_Receivables_ReceivableId",
                        column: x => x.ReceivableId,
                        principalTable: "Receivables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_CashShiftId",
                table: "CustomerPayments",
                column: "CashShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_Customer",
                table: "CustomerPayments",
                columns: new[] { "CustomerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_Number",
                table: "CustomerPayments",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_RequestId",
                table: "CustomerPayments",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_VoidCashShiftId",
                table: "CustomerPayments",
                column: "VoidCashShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Active_Search",
                table: "Customers",
                columns: new[] { "IsActive", "SearchText" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TaxId",
                table: "Customers",
                column: "TaxId",
                unique: true,
                filter: "\"TaxId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivableEntries_Payment",
                table: "ReceivableEntries",
                column: "CustomerPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivableEntries_Receivable",
                table: "ReceivableEntries",
                columns: new[] { "ReceivableId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Receivables_Customer_Status",
                table: "Receivables",
                columns: new[] { "CustomerId", "Status", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Receivables_SaleId",
                table: "Receivables",
                column: "SaleId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerPayments");

            migrationBuilder.DropTable(
                name: "ReceivableEntries");

            migrationBuilder.DropTable(
                name: "Receivables");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropColumn(
                name: "CustomerPaymentVoidsCashCents",
                table: "CashShifts");

            migrationBuilder.DropColumn(
                name: "CustomerPaymentVoidsNonCashCents",
                table: "CashShifts");

            migrationBuilder.DropColumn(
                name: "CustomerPaymentsCashCents",
                table: "CashShifts");

            migrationBuilder.DropColumn(
                name: "CustomerPaymentsNonCashCents",
                table: "CashShifts");

            migrationBuilder.DropColumn(
                name: "OnAccountSalesCents",
                table: "CashShifts");
        }
    }
}
