using Pos.Application.Abstractions;
using Pos.Application.Inventory;
using Pos.Application.Products;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Sales.ReviewSale;

/// <summary>Solo lee: no guarda nada (research §5).</summary>
public sealed class ReviewSaleHandler
{
    private readonly IAccessControl _access;
    private readonly IProductRepository _products;
    private readonly IInventoryRepository _inventory;

    public ReviewSaleHandler(IAccessControl access, IProductRepository products, IInventoryRepository inventory)
    {
        _access = access;
        _products = products;
        _inventory = inventory;
    }

    public async Task<Result<SaleReview>> HandleAsync(ReviewSaleQuery query, CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(Permission.Sell, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<SaleReview>(access.Error!);
        }

        ArgumentNullException.ThrowIfNull(query);

        var ids = query.Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = (await _products.GetManyAsync(ids, includeDeleted: true, cancellationToken)).ToDictionary(p => p.Id);
        var tracked = products.Values.Where(p => p.TracksInventory).Select(p => p.Id).ToList();
        var stocks = tracked.Count == 0
            ? new Dictionary<Guid, Pos.Domain.Inventory.ProductStock>()
            : (await _inventory.GetStocksAsync(tracked, cancellationToken)).ToDictionary(s => s.Key, s => s.Value);

        var lines = query.Lines
            .Select(l => SaleReviewer.Review(l.ProductId, l.QuantityThousandths, products.GetValueOrDefault(l.ProductId), stocks))
            .ToList();
        return Result.Success(new SaleReview(lines));
    }
}
