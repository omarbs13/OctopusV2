namespace Pos.Application.Products.CreateProduct;

/// <summary>
/// Alta de producto; los importes llegan como texto capturado ("1234.50" o "1,234.50"). Una imagen
/// nula equivale a <see cref="ProductImageChange.KeepCurrent"/> (sin imagen). <c>CategoryId</c> nulo =
/// "Sin categoría"; si no es nulo debe ser una categoría activa (016, FR-011). <c>ReorderPointText</c> vacío
/// = sin punto de reorden (022). <c>Sku</c> vacío genera un código único (026); <c>PackagingText</c> vacío o
/// "NA" = el embalaje no aplica; <c>SupplierId</c>, si se indica, debe ser un proveedor activo.
/// </summary>
public sealed record CreateProductCommand(
    string Model,
    string? Sku,
    string? Barcode,
    string UnitCode,
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
