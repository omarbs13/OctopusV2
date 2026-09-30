using Pos.Application.Abstractions;

namespace Pos.Application.Products.GetProduct;

/// <summary>Datos actuales de un producto no borrado; se usa al abrir el editor y al recargar tras un conflicto.</summary>
public sealed class GetProductHandler
{
    private readonly IProductRepository _products;

    public GetProductHandler(IProductRepository products) => _products = products;

    public async Task<Result<ProductDto>> HandleAsync(GetProductQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var product = await _products.GetAsync(query.Id, includeImage: true, cancellationToken);
        return product is null
            ? Result.Failure<ProductDto>(new NotFound())
            : Result.Success(product.ToDto());
    }
}
