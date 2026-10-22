namespace Pos.Application.Products.UpdateProduct;

/// <summary>
/// Edición de producto; <paramref name="ExpectedVersion"/> es la versión que vio el operador.
/// <c>CategoryId</c> nulo = "Sin categoría"; una categoría inactiva solo se conserva si no cambia (016, FR-011).
/// <c>ReorderPointText</c> vacío = sin punto de reorden (022). Los campos del proveedor son los del alta (026);
/// un proveedor inactivo solo se conserva si no cambia.
/// </summary>
public sealed record UpdateProductCommand(
    Guid Id,
    int ExpectedVersion,
    string Model,
    string? Sku,
    string? Barcode,
    string UnitCode,
    bool IsActive,
    string? PiecePriceText = null,
    string? PricePerSqmText = null,
    string? PricePerBoxText = null,
    string? PackagingText = null,
    string? PiecesPerBoxText = null,
    string? Measures = null,
    string? Brand = null,
    Guid? SupplierId = null,
    ProductImageChange? Image = null,
    bool TracksInventory = false,
    string? MinimumStockText = null,
    Guid? CategoryId = null,
    string? ReorderPointText = null) : IProductInput;
