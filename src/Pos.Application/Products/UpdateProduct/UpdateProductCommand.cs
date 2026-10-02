namespace Pos.Application.Products.UpdateProduct;

/// <summary>
/// Edición de producto; <paramref name="ExpectedVersion"/> es la versión que vio el operador.
/// <c>CategoryId</c> nulo = "Sin categoría"; una categoría inactiva solo se conserva si no cambia (016, FR-011).
/// </summary>
public sealed record UpdateProductCommand(
    Guid Id,
    int ExpectedVersion,
    string Name,
    string Sku,
    string? Barcode,
    string PriceText,
    string UnitCode,
    bool IsActive,
    ProductImageChange? Image = null,
    bool TracksInventory = false,
    string? MinimumStockText = null,
    Guid? CategoryId = null);
