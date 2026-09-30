using Pos.Application.Abstractions;
using Pos.Application.Inventory;
using Pos.Application.Products;
using Pos.Domain.Common;
using Pos.Domain.Products;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Sales.FindProductsForSale;

/// <summary>
/// Busca productos para vender (research §8): primero coincidencia exacta de código de barras o SKU,
/// que incluye inactivos y borrados para informar el motivo; si no hay, búsqueda por nombre.
/// </summary>
public sealed class FindProductsForSaleHandler
{
    public const int NameMatchLimit = 20;

    private readonly IAccessControl _access;
    private readonly IProductRepository _products;
    private readonly IInventoryRepository _inventory;

    public FindProductsForSaleHandler(IAccessControl access, IProductRepository products, IInventoryRepository inventory)
    {
        _access = access;
        _products = products;
        _inventory = inventory;
    }

    public async Task<Result<ProductLookup>> HandleAsync(FindProductsForSaleQuery query, CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(Permission.Sell, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<ProductLookup>(access.Error!);
        }

        ArgumentNullException.ThrowIfNull(query);

        var text = query.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return Result.Failure<ProductLookup>(new ValidationFailed(
                [new FieldError(SaleFields.Text, SaleMessages.TextRequired)]));
        }

        var exact = await _products.FindForSaleAsync(text, cancellationToken);

        // Un producto borrado solo cuenta si ninguno vigente usa el mismo código.
        var matches = exact.Any(p => !p.IsDeleted) ? [.. exact.Where(p => !p.IsDeleted)] : exact.ToList();
        if (matches.Count > 0)
        {
            var kind = matches.Count == 1 ? LookupKind.ExactMatch : LookupKind.NameMatches;
            return Result.Success(new ProductLookup(kind, await MapAsync(matches, cancellationToken)));
        }

        var byName = await _products.SearchForSaleAsync(TextNormalizer.ForSearch(text), NameMatchLimit, cancellationToken);
        return byName.Count == 0
            ? Result.Success(new ProductLookup(LookupKind.None, []))
            : Result.Success(new ProductLookup(LookupKind.NameMatches, await MapAsync(byName, cancellationToken)));
    }

    private async Task<IReadOnlyList<SaleProductDto>> MapAsync(IReadOnlyList<Product> products, CancellationToken cancellationToken)
    {
        var tracked = products.Where(p => p.TracksInventory).Select(p => p.Id).ToList();
        var stocks = tracked.Count == 0
            ? new Dictionary<Guid, Pos.Domain.Inventory.ProductStock>()
            : (await _inventory.GetStocksAsync(tracked, cancellationToken)).ToDictionary(s => s.Key, s => s.Value);
        return [.. products.Select(p => SaleProductMapping.ToDto(p, stocks.GetValueOrDefault(p.Id)))];
    }
}
