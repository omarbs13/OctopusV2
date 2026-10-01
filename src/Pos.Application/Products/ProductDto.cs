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
    bool IsCritical = false);

/// <summary>Fila del listado de productos.</summary>
public sealed record ProductListItemDto(
    Guid Id,
    string Name,
    string Sku,
    string? Barcode,
    long PriceCents,
    string UnitCode,
    string UnitName,
    bool IsActive,
    int Version,
    byte[]? Thumbnail = null,
    bool TracksInventory = false,
    long? OnHandThousandths = null,
    int DecimalPlaces = 0);
