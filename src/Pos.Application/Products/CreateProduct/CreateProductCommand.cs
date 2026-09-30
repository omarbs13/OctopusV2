namespace Pos.Application.Products.CreateProduct;

/// <summary>
/// Alta de producto; el precio llega como texto capturado ("1234.50" o "1,234.50"). Una imagen
/// nula equivale a <see cref="ProductImageChange.KeepCurrent"/> (sin imagen).
/// </summary>
public sealed record CreateProductCommand(
    string Name,
    string Sku,
    string? Barcode,
    string PriceText,
    string UnitCode,
    ProductImageChange? Image = null);
