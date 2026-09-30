using Pos.Application.Abstractions;

namespace Pos.Application.Sales.GetSalesDashboard;

/// <summary>Datos de las tarjetas de ventas de Inicio; solo cuenta ventas completadas (FR-036).</summary>
public sealed class GetSalesDashboardHandler
{
    private readonly ISaleRepository _sales;

    public GetSalesDashboardHandler(ISaleRepository sales) => _sales = sales;

    public async Task<Result<SalesDashboard>> HandleAsync(SalesDashboardQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return Result.Success(await _sales.GetDashboardAsync(query.Days, cancellationToken));
    }
}
