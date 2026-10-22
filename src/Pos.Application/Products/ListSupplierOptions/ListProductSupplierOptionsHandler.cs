using Pos.Application.Abstractions;
using Pos.Application.Suppliers;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Products.ListSupplierOptions;

/// <summary>
/// Proveedores que se pueden asignar a un producto (026): los activos y, al editar, el que el producto ya
/// tenía aunque esté inactivo.
/// </summary>
public sealed class ListProductSupplierOptionsHandler
{
    public const int Limit = 500;

    private readonly IAccessControl _access;
    private readonly ISupplierRepository _suppliers;

    public ListProductSupplierOptionsHandler(IAccessControl access, ISupplierRepository suppliers)
    {
        _access = access;
        _suppliers = suppliers;
    }

    public async Task<Result<IReadOnlyList<SupplierOption>>> HandleAsync(Guid? currentSupplierId, CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(Permission.ManageProducts, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<IReadOnlyList<SupplierOption>>(access.Error!);
        }

        var options = (await _suppliers.ListActiveAsync(null, Limit, cancellationToken)).ToList();
        if (currentSupplierId is { } id && options.All(o => o.Id != id)
            && await _suppliers.GetAsync(id, cancellationToken) is { } current)
        {
            options.Insert(0, new SupplierOption(current.Id, current.Name, current.TaxId));
        }

        return Result.Success<IReadOnlyList<SupplierOption>>(options);
    }
}
