using Pos.Application.Abstractions;
using Pos.Domain.Common;
using Pos.Domain.Products;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Products.SearchProducts;

/// <summary>
/// Busca productos visibles por páginas (FR-006 a FR-012 de 003; FR-016 y FR-017 de 001): nombre
/// sin acentos ni mayúsculas y SKU por coincidencia parcial; código de barras exacto si el texto
/// es un código completo.
/// </summary>
public sealed class SearchProductsHandler
{
    private readonly IAccessControl _access;
    private readonly IProductRepository _products;

    public SearchProductsHandler(IAccessControl access, IProductRepository products)
    {
        _access = access;
        _products = products;
    }

    public async Task<Result<ProductPage>> HandleAsync(SearchProductsQuery query, CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(Permission.ViewProducts, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<ProductPage>(access.Error!);
        }

        ArgumentNullException.ThrowIfNull(query);

        var text = query.Text?.Trim();
        var page = Math.Max(query.Page, 1);
        var pageSize = ProductPage.DefaultPageSize;
        var search = string.IsNullOrEmpty(text)
            ? new ProductSearch(null, null, null, BarcodeExact: false, query.IncludeInactive, page, pageSize, query.Category)
            : new ProductSearch(
                TextNormalizer.ForSearch(text),
                text.ToUpperInvariant(),
                text,
                Product.LooksLikeFullBarcode(text),
                query.IncludeInactive,
                page,
                pageSize,
                query.Category);

        if (query.LocateProductId is { } productId
            && await _products.LocatePageAsync(search, productId, cancellationToken) is { } located)
        {
            search = search with { Page = located };
        }

        return Result.Success(await _products.SearchAsync(search, cancellationToken));
    }
}
