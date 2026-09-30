using Pos.Domain.Products;

namespace Pos.Application.Products;

public static class ProductMapping
{
    public static ProductDto ToDto(this Product product)
    {
        ArgumentNullException.ThrowIfNull(product);
        return new ProductDto(
            product.Id,
            product.Name,
            product.Sku,
            product.Barcode,
            product.Price.Cents,
            product.UnitCode,
            product.IsActive,
            product.Version,
            product.CreatedAt,
            product.UpdatedAt,
            product.Image?.Content);
    }
}
