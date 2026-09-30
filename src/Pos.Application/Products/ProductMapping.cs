using Pos.Domain.Inventory;
using Pos.Domain.Products;

namespace Pos.Application.Products;

public static class ProductMapping
{
    /// <param name="product">Producto a convertir.</param>
    /// <param name="stock">Existencia del producto, o nula si no tiene movimientos.</param>
    public static ProductDto ToDto(this Product product, ProductStock? stock = null)
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
            product.Image?.Content,
            product.TracksInventory,
            product.MinimumStockThousandths,
            product.TracksInventory ? stock?.OnHandThousandths ?? 0 : null,
            stock is not null,
            UnitOfMeasure.Find(product.UnitCode)?.DecimalPlaces ?? 0);
    }
}
