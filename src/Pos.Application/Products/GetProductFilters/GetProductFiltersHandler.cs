using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Products.GetProductFilters;

/// <summary>Valores para los filtros "Marca" y "Medida" del listado: los distintos de los productos no borrados (026).</summary>
public sealed class GetProductFiltersHandler
{
    private readonly IAccessControl _access;
    private readonly IProductRepository _products;

    public GetProductFiltersHandler(IAccessControl access, IProductRepository products)
    {
        _access = access;
        _products = products;
    }

    public async Task<Result<ProductFilterOptions>> HandleAsync(CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(Permission.ViewProducts, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<ProductFilterOptions>(access.Error!);
        }

        return Result.Success(await _products.GetFilterOptionsAsync(cancellationToken));
    }
}
