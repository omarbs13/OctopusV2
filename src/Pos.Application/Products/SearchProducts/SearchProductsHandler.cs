using Pos.Application.Abstractions;
using Pos.Domain.Common;
using Pos.Domain.Products;

namespace Pos.Application.Products.SearchProducts;

/// <summary>
/// Busca productos visibles (FR-016 y FR-017): nombre sin acentos ni mayúsculas y SKU por
/// coincidencia parcial; código de barras exacto si el texto es un código completo.
/// </summary>
public sealed class SearchProductsHandler
{
    public const int Limit = 200;

    private readonly IProductRepository _products;

    public SearchProductsHandler(IProductRepository products) => _products = products;

    public async Task<Result<SearchProductsResult>> HandleAsync(SearchProductsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var text = query.Text?.Trim();
        var search = string.IsNullOrEmpty(text)
            ? new ProductSearch(null, null, null, BarcodeExact: false, query.IncludeInactive, Limit)
            : new ProductSearch(
                TextNormalizer.ForSearch(text),
                text.ToUpperInvariant(),
                text,
                Product.LooksLikeFullBarcode(text),
                query.IncludeInactive,
                Limit);

        var page = await _products.SearchAsync(search, cancellationToken);
        return Result.Success(new SearchProductsResult(page.Items, page.HasMore));
    }
}
