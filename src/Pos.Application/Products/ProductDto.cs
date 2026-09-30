namespace Pos.Application.Products;

/// <summary>Producto completo; el precio viaja en centavos y la imagen, optimizada (WEBP).</summary>
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
    byte[]? Image = null);

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
    byte[]? Thumbnail = null);
