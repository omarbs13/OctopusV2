namespace Pos.Application.Products.UpdateProduct;

/// <summary>Edición de producto; <paramref name="ExpectedVersion"/> es la versión que vio el operador.</summary>
public sealed record UpdateProductCommand(
    Guid Id,
    int ExpectedVersion,
    string Name,
    string Sku,
    string? Barcode,
    string PriceText,
    string UnitCode,
    bool IsActive,
    ProductImageChange? Image = null);
