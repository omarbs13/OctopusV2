namespace Pos.Application.Products.CreateProduct;

/// <summary>
/// Alta de producto; el precio llega como texto capturado ("1234.50" o "1,234.50"). Una imagen
/// nula equivale a <see cref="ProductImageChange.KeepCurrent"/> (sin imagen). <c>CategoryId</c> nulo =
/// "Sin categoría"; si no es nulo debe ser una categoría activa (016, FR-011). <c>ReorderPointText</c> vacío
/// = sin punto de reorden (022).
/// </summary>
public sealed record CreateProductCommand(
    string Name,
    string Sku,
    string? Barcode,
    string PriceText,
    string UnitCode,
    ProductImageChange? Image = null,
    bool TracksInventory = false,
    string? MinimumStockText = null,
    Guid? CategoryId = null,
    string? ReorderPointText = null);
