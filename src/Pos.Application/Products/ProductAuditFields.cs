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

    public static IReadOnlyList<AuditField> Snapshot(
        Product product,
        string? categoryName,
        bool hasImage,
        string? supplierName = null)
    {
        ArgumentNullException.ThrowIfNull(product);
        var unit = UnitOfMeasure.Find(product.UnitCode);
        return
        [
            new("SKU", product.Sku),
            new("Código de barras", product.Barcode),
            new("Modelo", product.Model),
            new("Medidas", product.Measures),
            new("Marca", product.Brand),
            new("Proveedor", product.SupplierId is null ? null : supplierName ?? product.SupplierId.Value.ToString()),
            new("Embalaje (m²/caja)", Packaging(product.PackagingThousandths)),
            new("Piezas por caja", product.PiecesPerBox?.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("Precio X M2", Cents(product.PricePerSqmCents)),
            new("Precio X CAJA", Cents(product.PricePerBoxCents)),
            new("Precio por pieza", Cents(product.PiecePriceCents)),
            new("Unidad de medida", unit?.Name ?? product.UnitCode),
            new("Categoría", product.CategoryId is null ? AuditFormat.NoCategory : AuditFormat.Category(categoryName)),
            new("Maneja inventario", AuditFormat.YesNo(product.TracksInventory)),
            new("Existencia mínima", AuditFormat.Quantity(product.MinimumStock, unit?.DecimalPlaces ?? 0)),
            new("Punto de reorden", AuditFormat.Quantity(product.ReorderPoint, unit?.DecimalPlaces ?? 0)),
            new(Critical, AuditFormat.YesNo(product.IsCritical)),
            new("Estado", AuditFormat.ActiveState(product.IsActive)),
            new("Para revisión", AuditFormat.YesNo(product.NeedsReview)),
            new(Image, hasImage ? WithImage : WithoutImage),
        ];
    }

    private static string? Cents(long? cents) => cents is { } c ? AuditFormat.Money(Pos.Domain.Common.Money.FromCents(c)) : null;

    private static string? Packaging(long? thousandths) =>
        thousandths is { } t ? Pos.Domain.Common.Quantity.FromThousandths(t).ToEditableString(3) : null;
}
