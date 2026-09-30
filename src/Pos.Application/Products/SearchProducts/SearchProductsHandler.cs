using Pos.Application.Abstractions;
using Pos.Domain.Common;
using Pos.Domain.Products;

namespace Pos.Application.Products.SearchProducts;

/// <summary>
/// Busca productos visibles por páginas (FR-006 a FR-012 de 003; FR-016 y FR-017 de 001): nombre
/// sin acentos ni mayúsculas y SKU por coincidencia parcial; código de barras exacto si el texto
/// es un código completo.
/// </summary>
public sealed class SearchProductsHandler
{
    private readonly IProductRepository _products;

    public SearchProductsHandler(IProductRepository products) => _products = products;

    public async Task<Result<ProductPage>> HandleAsync(SearchProductsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var text = query.Text?.Trim();
        var page = Math.Max(query.Page, 1);
        var pageSize = ProductPage.DefaultPageSize;
        var search = string.IsNullOrEmpty(text)
            ? new ProductSearch(null, null, null, BarcodeExact: false, query.IncludeInactive, page, pageSize)
            : new ProductSearch(
                TextNormalizer.ForSearch(text),
                text.ToUpperInvariant(),
                text,
                Product.LooksLikeFullBarcode(text),
                query.IncludeInactive,
                page,
                pageSize);

        if (query.LocateProductId is { } productId
            && await _products.LocatePageAsync(search, productId, cancellationToken) is { } located)
        {
            search = search with { Page = located };
        }

        return Result.Success(await _products.SearchAsync(search, cancellationToken));
    }
}
