using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Pos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProductCatalogImprovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_NameSearch",
                table: "Products");

            migrationBuilder.AddColumn<string>(
                name: "UnitCode",
                table: "Products",
                type: "TEXT",
                maxLength: 3,
                nullable: false,
                defaultValue: "H87");

            migrationBuilder.CreateTable(
                name: "ProductImages",
                columns: table => new
                {
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Content = table.Column<byte[]>(type: "BLOB", nullable: false),
                    Thumbnail = table.Column<byte[]>(type: "BLOB", nullable: false),
                    Width = table.Column<int>(type: "INTEGER", nullable: false),
                    Height = table.Column<int>(type: "INTEGER", nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductImages", x => x.ProductId);
                    table.ForeignKey(
                        name: "FK_ProductImages_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UnitsOfMeasure",
                columns: table => new
                {
                    Code = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitsOfMeasure", x => x.Code);
                });

            migrationBuilder.InsertData(
                table: "UnitsOfMeasure",
                columns: new[] { "Code", "Name", "SortOrder" },
                values: new object[,]
                {
                    { "GRM", "Gramo", 3 },
                    { "H87", "Pieza", 1 },
                    { "KGM", "Kilogramo", 2 },
                    { "LTR", "Litro", 4 },
                    { "MLT", "Mililitro", 5 },
                    { "MTR", "Metro", 6 },
                    { "XBX", "Caja", 7 },
                    { "XPK", "Paquete", 8 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_NameSearch",
                table: "Products",
                columns: new[] { "NameSearch", "Sku" },
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Products_UnitCode",
                table: "Products",
                column: "UnitCode");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_UnitsOfMeasure_UnitCode",
                table: "Products",
                column: "UnitCode",
                principalTable: "UnitsOfMeasure",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_UnitsOfMeasure_UnitCode",
                table: "Products");

            migrationBuilder.DropTable(
                name: "ProductImages");

            migrationBuilder.DropTable(
                name: "UnitsOfMeasure");

            migrationBuilder.DropIndex(
                name: "IX_Products_NameSearch",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_UnitCode",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "UnitCode",
                table: "Products");

            migrationBuilder.CreateIndex(
                name: "IX_Products_NameSearch",
                table: "Products",
                column: "NameSearch",
                filter: "\"DeletedAt\" IS NULL");
        }
    }
}
