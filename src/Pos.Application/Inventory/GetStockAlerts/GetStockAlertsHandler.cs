using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Inventory.GetStockAlerts;

/// <summary>Conteos de existencia baja y sin existencia para las tarjetas de Inicio (FR-021).</summary>
public sealed class GetStockAlertsHandler
{
    private readonly IAccessControl _access;
    private readonly IInventoryRepository _inventory;

    public GetStockAlertsHandler(IAccessControl access, IInventoryRepository inventory)
    {
        _access = access;
        _inventory = inventory;
    }

    public async Task<Result<StockAlertCounts>> HandleAsync(CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(Permission.ViewInventory, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<StockAlertCounts>(access.Error!);
        }

        return Result.Success(await _inventory.CountAlertsAsync(cancellationToken));
    }
}
