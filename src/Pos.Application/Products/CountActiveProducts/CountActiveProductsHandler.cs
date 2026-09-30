using Pos.Application.Abstractions;

namespace Pos.Application.Products.CountActiveProducts;

/// <summary>Número de productos activos y no borrados (indicador de Inicio).</summary>
public sealed class CountActiveProductsHandler
{
    private readonly IProductRepository _products;

    public CountActiveProductsHandler(IProductRepository products) => _products = products;

    public async Task<Result<long>> HandleAsync(CancellationToken cancellationToken) =>
        Result.Success(await _products.CountActiveAsync(cancellationToken));
}
