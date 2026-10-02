using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Purchases.CalculatePurchaseTotals;

/// <summary>
/// Importes de línea, bonificaciones, subtotal, impuestos y total de la captura, con errores por línea y
/// generales (research §4). Lo invoca la interfaz en cada cambio; no lee ni escribe compras.
/// </summary>
public sealed class CalculatePurchaseTotalsHandler
{
    private readonly IAccessControl _access;

    public CalculatePurchaseTotalsHandler(IAccessControl access) => _access = access;

    public async Task<Result<PurchaseTotalsDto>> HandleAsync(CalculatePurchaseTotalsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var access = await _access.CheckAsync(Permission.RegisterPurchases, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<PurchaseTotalsDto>(access.Error!);
        }

        var calculation = PurchaseCalculator.Calculate(
            [.. query.Lines.Select(l => new PurchaseCalculationLine(l.DecimalPlaces, l.UnitName, l.QuantityText, l.UnitCostText))],
            query.TaxText);
        return Result.Success(calculation.ToDto());
    }
}
