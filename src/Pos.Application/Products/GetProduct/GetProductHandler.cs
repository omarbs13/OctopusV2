using Pos.Application.Abstractions;
using Pos.Application.Inventory;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Products.GetProduct;

/// <summary>Datos actuales de un producto no borrado; se usa al abrir el editor y al recargar tras un conflicto.</summary>
public sealed class GetProductHandler
{
    private readonly IAccessControl _access;
    private readonly IProductRepository _products;
    private readonly IInventoryRepository _inventory;

    public GetProductHandler(IAccessControl access, IProductRepository products, IInventoryRepository inventory)
    {
        _access = access;
        _products = products;
        _inventory = inventory;
    }

    public async Task<Result<ProductDto>> HandleAsync(GetProductQuery query, CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(Permission.ViewProducts, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<ProductDto>(access.Error!);
        }

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
