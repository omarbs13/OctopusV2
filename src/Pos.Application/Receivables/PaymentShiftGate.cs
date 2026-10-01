using Pos.Application.Abstractions;
using Pos.Application.CashShifts;
using Pos.Application.Licensing;
using Pos.Application.Sales;
using Pos.Application.Users.Access;
using Pos.Domain.CashShifts;
using Pos.Domain.Licensing;
using Pos.Domain.Users;

namespace Pos.Application.Receivables;

/// <summary>
/// Reglas de turno de un abono o de su anulación (research §5–§7, FR-012, FR-014): exige un turno
/// abierto utilizable (el propio, salvo <c>ManageShifts</c>) con cualquier forma de pago y, si sale
/// efectivo, que el esperado alcance sin revelar montos. Con Turnos sin licencia no hay turno que
/// controlar y el abono se registra sin turno, igual que las ventas (012).
/// </summary>
public sealed class PaymentShiftGate
{
    private readonly ICashShiftRepository _shifts;
    private readonly ISaleRepository _sales;
    private readonly IAccessControl _access;
    private readonly ICurrentUser _currentUser;
    private readonly ShiftGuard _guard;
    private readonly ILicenseState? _license;

    public PaymentShiftGate(
        ICashShiftRepository shifts,
        ISaleRepository sales,
        IAccessControl access,
        ICurrentUser currentUser,
        ShiftGuard guard,
        ILicenseState? license = null)
    {
        _shifts = shifts;
        _sales = sales;
        _access = access;
        _currentUser = currentUser;
        _guard = guard;
        _license = license;
    }

    /// <summary>
    /// El turno abierto en que se registra la operación; nulo solo si Turnos no tiene licencia.
    /// <paramref name="cashOutCents"/> es el efectivo que saldría de la caja (anulación de un abono en efectivo).
    /// </summary>
    public async Task<Result<CashShift?>> ResolveAsync(long cashOutCents, CancellationToken cancellationToken)
    {
        if (_license?.IsModuleActive(LicensedModule.CashShifts) == false)
        {
            return Result.Success<CashShift?>(null);
        }

        var shift = await _shifts.GetOpenAsync(CashRegister.Default, cancellationToken);
        if (shift is null)
        {
            return Result.Failure<CashShift?>(new ShiftRequired());
        }

        if (shift.OpenedBy != _currentUser.UserId && !await _access.HasAsync(Permission.ManageShifts, cancellationToken))
        {
            return Result.Failure<CashShift?>(new ShiftOwnedByOther(await _guard.NameOfAsync(shift.OpenedBy, cancellationToken)));
        }

        if (cashOutCents > 0)
        {
            var expected = shift.ExpectedCash(await _sales.GetShiftTotalsAsync(shift.Id, cancellationToken));
            if (!CashShiftMath.CanRefund(expected, cashOutCents))
            {
                return Result.Failure<CashShift?>(new InsufficientCash(null));
            }
        }

        return Result.Success<CashShift?>(shift);
    }
}
