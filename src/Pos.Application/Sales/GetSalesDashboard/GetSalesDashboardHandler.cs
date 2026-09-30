using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Sales.GetSalesDashboard;

/// <summary>Datos de las tarjetas de ventas de Inicio; solo cuenta ventas completadas (FR-036).</summary>
public sealed class GetSalesDashboardHandler
{
    private readonly IAccessControl _access;
    private readonly ISaleRepository _sales;

    public GetSalesDashboardHandler(IAccessControl access, ISaleRepository sales)
    {
        _access = access;
        _sales = sales;
    }

    public async Task<Result<SalesDashboard>> HandleAsync(SalesDashboardQuery query, CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(Permission.ViewAllSales, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<SalesDashboard>(access.Error!);
        }

        ArgumentNullException.ThrowIfNull(query);
        return Result.Success(await _sales.GetDashboardAsync(query.Days, cancellationToken));
    }
}
