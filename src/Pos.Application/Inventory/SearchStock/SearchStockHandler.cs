using Pos.Application.Abstractions;
using Pos.Domain.Common;
using Pos.Domain.Products;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Inventory.SearchStock;

/// <summary>
/// Lista los productos que controlan inventario (FR-016, FR-017). El texto se normaliza igual que
/// en la búsqueda de productos: nombre sin acentos, SKU en mayúsculas y código de barras exacto si
/// el texto es un código completo.
/// </summary>
public sealed class SearchStockHandler
{
    private readonly IAccessControl _access;
    private readonly IInventoryRepository _inventory;

    public SearchStockHandler(IAccessControl access, IInventoryRepository inventory)
    {
        _access = access;
        _inventory = inventory;
    }

    public async Task<Result<StockPage>> HandleAsync(SearchStockQuery query, CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(Permission.ViewInventory, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<StockPage>(access.Error!);
        }

        ArgumentNullException.ThrowIfNull(query);

        var text = query.Text?.Trim();
        var page = Math.Max(query.Page, 1);
        var search = string.IsNullOrEmpty(text)
            ? new StockSearch(null, null, null, BarcodeExact: false, query.Filter, query.IncludeInactive, page, StockPage.DefaultPageSize)
            : new StockSearch(
                TextNormalizer.ForSearch(text),
                text.ToUpperInvariant(),
                text,
                Product.LooksLikeFullBarcode(text),
                query.Filter,
                query.IncludeInactive,
                page,
                StockPage.DefaultPageSize);

        return Result.Success(await _inventory.SearchStockAsync(search, cancellationToken));
    }
}
