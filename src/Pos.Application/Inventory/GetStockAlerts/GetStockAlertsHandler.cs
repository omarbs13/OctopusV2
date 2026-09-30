using Pos.Application.Abstractions;

namespace Pos.Application.Inventory.GetStockAlerts;

/// <summary>Conteos de existencia baja y sin existencia para las tarjetas de Inicio (FR-021).</summary>
public sealed class GetStockAlertsHandler
{
    private readonly IInventoryRepository _inventory;

    public GetStockAlertsHandler(IInventoryRepository inventory) => _inventory = inventory;

    public async Task<Result<StockAlertCounts>> HandleAsync(CancellationToken cancellationToken) =>
        Result.Success(await _inventory.CountAlertsAsync(cancellationToken));
}
