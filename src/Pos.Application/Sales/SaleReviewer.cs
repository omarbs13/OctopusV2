using Pos.Domain.Common;
using Pos.Domain.Inventory;
using Pos.Domain.Products;
using Pos.Domain.Sales;

namespace Pos.Application.Sales;

/// <summary>Revisión de una línea contra el producto y la existencia vigentes (research §5).</summary>
internal static class SaleReviewer
{
    public static SaleLineReview Review(
        Guid productId,
        long quantityThousandths,
        Product? product,
        IReadOnlyDictionary<Guid, ProductStock> stocks,
        bool checkStock = true,
        SaleMode? mode = null)
    {
        if (product is null)
        {
            return new SaleLineReview(productId, 0, NotSellableReason.Deleted, false, null);
        }

        // 026: el precio y la existencia dependen de la modalidad; una que ya no se ofrece queda sin precio
        // (cambió el producto) y la venta se revisa.
        var saleMode = mode ?? product.InitialMode;
        var modeAvailable = saleMode == product.InitialMode || product.AvailableModes.Contains(saleMode);
        var price = modeAvailable ? product.PriceFor(saleMode) ?? 0 : 0;

        long? onHand = null;
        var insufficient = false;
        if (product.TracksInventory && checkStock)
        {
            var level = stocks.TryGetValue(product.Id, out var stock) ? stock.OnHand : StockLevel.Zero;
            onHand = level.Thousandths;
            var needed = product.ToProductUnitThousandths(saleMode, quantityThousandths);
            insufficient = needed is > 0
                && needed <= Quantity.MaxStockThousandths
                && ProductStock.IsShort(level, Quantity.FromThousandths(needed.Value));
        }

        return new SaleLineReview(product.Id, price, SaleProductMapping.ReasonOf(product), insufficient, onHand);
    }
}
