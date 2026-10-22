using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TileSupplierFieldsData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Datos (026). Va en una migración aparte porque TileSupplierFields reconstruye Products
            // (PriceCents pasa a nulable) y SQLite no permite mezclar SQL propio con esa reconstrucción.
            // El nombre anterior pasa a ser el modelo (completo, hasta 200 caracteres); los de más de 150
            // quedan "para revisión". El precio de los productos de caja pasa a X CAJA y el de los demás
            // sigue siendo el precio por pieza. Las claves las calcula ProductIdentityBackfill al arrancar.
            migrationBuilder.Sql(
                """
                UPDATE "Products"
                SET "Model" = "Name",
                    "NeedsReview" = CASE WHEN length("Name") > 150 THEN 1 ELSE 0 END,
                    "PricePerBoxCents" = CASE WHEN "UnitCode" = 'XBX' THEN "PriceCents" ELSE NULL END,
                    "PriceCents" = CASE WHEN "UnitCode" = 'XBX' THEN NULL ELSE "PriceCents" END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
