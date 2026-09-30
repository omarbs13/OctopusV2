using Pos.Application.Abstractions;
using Pos.Application.Inventory;

namespace Pos.Application.Products.GetProduct;

/// <summary>Datos actuales de un producto no borrado; se usa al abrir el editor y al recargar tras un conflicto.</summary>
public sealed class GetProductHandler
{
    private readonly IProductRepository _products;
    private readonly IInventoryRepository _inventory;

    public GetProductHandler(IProductRepository products, IInventoryRepository inventory)
    {
        _products = products;
        _inventory = inventory;
    }

    public async Task<Result<ProductDto>> HandleAsync(GetProductQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var product = await _products.GetAsync(query.Id, includeImage: true, cancellationToken);
        if (product is null)
        {
            return Result.Failure<ProductDto>(new NotFound());
        }

        var stock = await _inventory.GetStockAsync(product.Id, cancellationToken);
        return Result.Success(product.ToDto(stock));
    }
}
