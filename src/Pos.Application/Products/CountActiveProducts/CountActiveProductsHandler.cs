using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Products.CountActiveProducts;

/// <summary>Número de productos activos y no borrados (indicador de Inicio).</summary>
public sealed class CountActiveProductsHandler
{
    private readonly IAccessControl _access;
    private readonly IProductRepository _products;

    public CountActiveProductsHandler(IAccessControl access, IProductRepository products)
    {
        _access = access;
        _products = products;
    }

    public async Task<Result<long>> HandleAsync(CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(Permission.ViewProducts, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<long>(access.Error!);
        }

        return Result.Success(await _products.CountActiveAsync(cancellationToken));
    }
}
