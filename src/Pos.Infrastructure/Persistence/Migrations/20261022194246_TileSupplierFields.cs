using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TileSupplierFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "PriceCents",
                table: "Products",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<string>(
                name: "Brand",
                table: "Products",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BrandSearch",
                table: "Products",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdentityKey",
                table: "Products",
                type: "TEXT",
                maxLength: 289,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Measures",
                table: "Products",
                type: "TEXT",
                maxLength: 49,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MeasuresKey",
                table: "Products",
                type: "TEXT",
                maxLength: 49,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Model",
                table: "Products",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ModelKey",
                table: "Products",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "NeedsReview",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "PackagingThousandths",
                table: "Products",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PiecesPerBox",
                table: "Products",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PricePerBoxCents",
                table: "Products",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PricePerSqmCents",
                table: "Products",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierId",
                table: "Products",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductMigrationSummaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    MigratedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    WithoutModelCount = table.Column<int>(type: "INTEGER", nullable: false),
                    WithoutPackagingCount = table.Column<int>(type: "INTEGER", nullable: false),
                    DuplicateCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Shown = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductMigrationSummaries", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "UnitsOfMeasure",
                keyColumn: "Code",
                keyValue: "XBX",
                column: "DecimalPlaces",
                value: 3);

            migrationBuilder.InsertData(
                table: "UnitsOfMeasure",
                columns: new[] { "Code", "DecimalPlaces", "Name", "SortOrder" },
                values: new object[] { "MTK", 3, "Metro cuadrado", 9 });

            migrationBuilder.CreateIndex(
                name: "IX_Products_BrandSearch",
                table: "Products",
                column: "BrandSearch",
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Products_IdentityKey",
                table: "Products",
                column: "IdentityKey",
                unique: true,
                filter: "\"DeletedAt\" IS NULL AND \"IdentityKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Products_MeasuresKey",
                table: "Products",
                column: "MeasuresKey",
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Products_NeedsReview",
                table: "Products",
                column: "NeedsReview",
                filter: "\"DeletedAt\" IS NULL AND \"NeedsReview\" = 1");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductMigrationSummaries");

            migrationBuilder.DropIndex(
                name: "IX_Products_BrandSearch",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_IdentityKey",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_MeasuresKey",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_NeedsReview",
                table: "Products");

            migrationBuilder.DeleteData(
                table: "UnitsOfMeasure",
                keyColumn: "Code",
                keyValue: "MTK");

            migrationBuilder.DropColumn(
                name: "Brand",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "BrandSearch",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IdentityKey",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Measures",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MeasuresKey",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Model",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ModelKey",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "NeedsReview",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PackagingThousandths",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PiecesPerBox",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PricePerBoxCents",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PricePerSqmCents",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                table: "Products");

            migrationBuilder.AlterColumn<long>(
                name: "PriceCents",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "UnitsOfMeasure",
                keyColumn: "Code",
                keyValue: "XBX",
                column: "DecimalPlaces",
                value: 0);
        }
    }
}
