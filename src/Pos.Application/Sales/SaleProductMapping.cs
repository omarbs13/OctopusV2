using Pos.Domain.Inventory;
using Pos.Domain.Products;

namespace Pos.Application.Sales;

internal static class SaleProductMapping
{
    public static NotSellableReason? ReasonOf(Product product) =>
        product.IsDeleted ? NotSellableReason.Deleted
        : !product.IsActive ? NotSellableReason.Inactive
        : null;

    public static SaleProductDto ToDto(Product product, ProductStock? stock)
    {
        var unit = UnitOfMeasure.Find(product.UnitCode) ?? UnitOfMeasure.Default;
        return new SaleProductDto(
            product.Id,
            product.Name,
            product.Sku,
            product.Barcode,
            product.Price.Cents,
            unit.Code,
            unit.Name,
            unit.DecimalPlaces,
            product.TracksInventory,
            product.TracksInventory ? stock?.OnHandThousandths ?? 0 : null,
            ReasonOf(product));
    }
}
