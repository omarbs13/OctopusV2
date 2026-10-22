using Pos.Domain.Common;
using Pos.Domain.Inventory;
using Pos.Domain.Products;

namespace Pos.Application.Sales;

/// <summary>Revisión de una línea contra el producto y la existencia vigentes (research §5).</summary>
internal static class SaleReviewer
{
    public static SaleLineReview Review(
        Guid productId,
        long quantityThousandths,
        Product? product,
        IReadOnlyDictionary<Guid, ProductStock> stocks,
        bool checkStock = true)
    {
        if (product is null)
        {
            return new SaleLineReview(productId, 0, NotSellableReason.Deleted, false, null);
        }

        long? onHand = null;
        var insufficient = false;
        if (product.TracksInventory && checkStock)
        {
            var level = stocks.TryGetValue(product.Id, out var stock) ? stock.OnHand : StockLevel.Zero;
            onHand = level.Thousandths;
            insufficient = quantityThousandths > 0
                && quantityThousandths <= Quantity.MaxStockThousandths
                && ProductStock.IsShort(level, Quantity.FromThousandths(quantityThousandths));
        }

        return new SaleLineReview(product.Id, product.Price.Cents, SaleProductMapping.ReasonOf(product), insufficient, onHand);
    }
}
