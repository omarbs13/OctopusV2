using Pos.Domain.Inventory;
using Pos.Domain.Products;
using Pos.Domain.Sales;

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
            ReasonOf(product),
            TileOf(product));
    }

    /// <summary>Precios, embalaje y modalidades de un producto de loseta; nulo si no es de loseta (026).</summary>
    public static TileSaleInfo? TileOf(Product product) =>
        product.IsTile
            ? new TileSaleInfo(
                product.AvailableModes,
                product.PricePerSqmCents,
                product.PricePerBoxCents,
                product.PiecePriceCents,
                product.PackagingThousandths)
            : null;
}
