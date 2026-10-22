using Pos.Application.Audit;
using Pos.Domain.Products;

namespace Pos.Application.Products;

/// <summary>
/// Instantánea de auditoría del producto (018, FR-003): campos legibles en el orden de la pantalla. Sin
/// costo (FR-003a) ni campos técnicos (versión, fechas, texto de búsqueda); la imagen, sin su contenido.
/// </summary>
public static class ProductAuditFields
{
    public const string Image = "Imagen";
    public const string Critical = "Crítico";
    public const string WithImage = "Con imagen";
    public const string WithoutImage = "Sin imagen";
    public const string ImageReplaced = "Imagen reemplazada";

    public static IReadOnlyList<AuditField> Snapshot(Product product, string? categoryName, bool hasImage)
    {
        ArgumentNullException.ThrowIfNull(product);
        var unit = UnitOfMeasure.Find(product.UnitCode);
        return
        [
            new("SKU", product.Sku),
            new("Código de barras", product.Barcode),
            new("Nombre", product.Name),
            new("Precio", AuditFormat.Money(product.Price)),
            new("Unidad de medida", unit?.Name ?? product.UnitCode),
            new("Categoría", product.CategoryId is null ? AuditFormat.NoCategory : AuditFormat.Category(categoryName)),
            new("Maneja inventario", AuditFormat.YesNo(product.TracksInventory)),
            new("Existencia mínima", AuditFormat.Quantity(product.MinimumStock, unit?.DecimalPlaces ?? 0)),
            new("Punto de reorden", AuditFormat.Quantity(product.ReorderPoint, unit?.DecimalPlaces ?? 0)),
            new(Critical, AuditFormat.YesNo(product.IsCritical)),
            new("Estado", AuditFormat.ActiveState(product.IsActive)),
            new(Image, hasImage ? WithImage : WithoutImage),
        ];
    }
}
