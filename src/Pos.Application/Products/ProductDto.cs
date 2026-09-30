namespace Pos.Application.Products;

/// <summary>Producto completo; el precio viaja en centavos.</summary>
public sealed record ProductDto(
    Guid Id,
    string Name,
    string Sku,
    string? Barcode,
    long PriceCents,
    bool IsActive,
    int Version,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

/// <summary>Fila del listado de productos.</summary>
public sealed record ProductListItemDto(
    Guid Id,
    string Name,
    string Sku,
    string? Barcode,
    long PriceCents,
    bool IsActive,
    int Version);
