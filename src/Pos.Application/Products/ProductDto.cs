using Pos.Domain.Products;

namespace Pos.Application.Products;

/// <summary>
/// Producto completo; el precio viaja en centavos y la imagen, optimizada (WEBP). Las cantidades de
/// inventario viajan en milésimas; la existencia actual es de solo lectura (FR-005).
/// </summary>
public sealed record ProductDto(
    Guid Id,
    string Name,
    string Sku,
    string? Barcode,
    long PriceCents,
    string UnitCode,
    bool IsActive,
    int Version,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    byte[]? Image = null,
    bool TracksInventory = false,
    long? MinimumStockThousandths = null,
    long? OnHandThousandths = null,
    bool HasMovements = false,
    int DecimalPlaces = 0,
    bool IsCritical = false,
    Guid? CategoryId = null,
    string? CategoryName = null,
    bool CategoryIsActive = true,
    long? ReorderPointThousandths = null,
    string Model = "",
    string? Measures = null,
    string? Brand = null,
    Guid? SupplierId = null,
    long? PackagingThousandths = null,
    int? PiecesPerBox = null,
    long? PricePerSqmCents = null,
    long? PricePerBoxCents = null,
    long? PiecePriceCents = null,
    bool NeedsReview = false,
    IReadOnlyList<string>? Warnings = null);

/// <summary>
/// Fila del listado de productos; <c>CategoryName</c> nulo = "Sin categoría" (016). <c>PriceCents</c> es el
/// precio en la unidad del producto (026); nulo si no se puede determinar.
/// </summary>
public sealed record ProductListItemDto(
    Guid Id,
    string Name,
    string Sku,
    string? Barcode,
    string UnitCode,
    string UnitName,
    bool IsActive,
    int Version,
    byte[]? Thumbnail = null,
    bool TracksInventory = false,
    long? OnHandThousandths = null,
    int DecimalPlaces = 0,
    string? CategoryName = null,
    bool CategoryIsActive = true,
    string Model = "",
    string? Measures = null,
    string? Brand = null,
    long? PackagingThousandths = null,
    long? PricePerSqmCents = null,
    long? PricePerBoxCents = null,
    long? PiecePriceCents = null,
    bool NeedsReview = false)
{
    public long PriceCents =>
        ProductPricing.DisplayPrice(UnitCode, PricePerSqmCents, PricePerBoxCents, PiecePriceCents, PackagingThousandths) ?? 0;
}
