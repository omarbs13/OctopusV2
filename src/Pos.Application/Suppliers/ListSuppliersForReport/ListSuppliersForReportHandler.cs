using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Suppliers.ListSuppliersForReport;

/// <summary>Proveedores del filtro de "Reportes > Compras", incluidos los inactivos (Historia 3, escenario 5).</summary>
public sealed class ListSuppliersForReportHandler
{
    private readonly IAccessControl _access;
    private readonly ISupplierRepository _suppliers;

    public ListSuppliersForReportHandler(IAccessControl access, ISupplierRepository suppliers)
    {
        _access = access;
        _suppliers = suppliers;
    }

    public async Task<Result<IReadOnlyList<SupplierFilterOption>>> HandleAsync(CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(Permission.ViewPurchaseReport, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<IReadOnlyList<SupplierFilterOption>>(access.Error!);
        }

        return Result.Success(await _suppliers.ListForFilterAsync(cancellationToken));
    }
}
