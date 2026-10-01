using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DiscountsAndCoupons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "DiscountCents",
                table: "Sales",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "LineDiscountCents",
                table: "SaleLines",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "OrderDiscountCents",
                table: "SaleLines",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "OriginalAmountCents",
                table: "SaleLines",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            // 015 (research §5): en las ventas anteriores el importe original es el importe registrado.
            migrationBuilder.Sql("UPDATE \"SaleLines\" SET \"OriginalAmountCents\" = \"AmountCents\";");

            migrationBuilder.CreateTable(
                name: "Coupons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Mode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Value = table.Column<long>(type: "INTEGER", nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    UsageLimit = table.Column<int>(type: "INTEGER", nullable: true),
                    UsesCount = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Coupons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DiscountApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DraftId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequestedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    AuthorizedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    Scope = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ApprovedBasisPoints = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscountApprovals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SaleDiscounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SaleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SaleLineId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Mode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Value = table.Column<long>(type: "INTEGER", nullable: false),
                    AmountCents = table.Column<long>(type: "INTEGER", nullable: false),
                    CouponId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CouponCode = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    AppliedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    AuthorizedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleDiscounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaleDiscounts_Coupons_CouponId",
                        column: x => x.CouponId,
                        principalTable: "Coupons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleDiscounts_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Coupons_Code",
                table: "Coupons",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscountApprovals_Draft",
                table: "DiscountApprovals",
                columns: new[] { "DraftId", "RequestedBy" });

            migrationBuilder.CreateIndex(
                name: "IX_SaleDiscounts_Coupon",
                table: "SaleDiscounts",
                column: "CouponId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleDiscounts_CreatedAt",
                table: "SaleDiscounts",
                columns: new[] { "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SaleDiscounts_Sale",
                table: "SaleDiscounts",
                column: "SaleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscountApprovals");

            migrationBuilder.DropTable(
                name: "SaleDiscounts");

            migrationBuilder.DropTable(
                name: "Coupons");

            migrationBuilder.DropColumn(
                name: "DiscountCents",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "LineDiscountCents",
                table: "SaleLines");

            migrationBuilder.DropColumn(
                name: "OrderDiscountCents",
                table: "SaleLines");

            migrationBuilder.DropColumn(
                name: "OriginalAmountCents",
                table: "SaleLines");
        }
    }
}
