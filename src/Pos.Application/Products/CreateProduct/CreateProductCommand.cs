namespace Pos.Application.Products.CreateProduct;

/// <summary>Alta de producto; el precio llega como texto capturado ("1234.50").</summary>
public sealed record CreateProductCommand(string Name, string Sku, string? Barcode, string PriceText);
