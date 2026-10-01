using Pos.Application.Abstractions;
using Pos.Application.CashShifts;
using Pos.Application.Licensing;
using Pos.Application.Sales;
using Pos.Application.Users.Access;
using Pos.Domain.CashShifts;
using Pos.Domain.Licensing;
using Pos.Domain.Users;

namespace Pos.Application.Returns;

/// <summary>
/// Reglas de turno y efectivo de un reintegro en efectivo (research §6): exige un turno abierto
/// utilizable (el propio, salvo <c>ManageShifts</c>) y efectivo esperado suficiente, sin revelar montos.
/// Con Turnos sin licencia no hay turno que controlar (012).
/// </summary>
public sealed class ReturnCashGate
{
    private readonly ICashShiftRepository _shifts;
    private readonly ISaleRepository _sales;
    private readonly IAccessControl _access;
    private readonly ICurrentUser _currentUser;
    private readonly ShiftGuard _guard;
    private readonly ILicenseState? _license;

    public ReturnCashGate(
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
    /// El turno abierto en que se registra el reintegro (nulo si no hay o Turnos no tiene licencia),
    /// o el error que impide devolver efectivo. Sin reintegro en efectivo nunca falla.
    /// </summary>
    public async Task<Result<CashShift?>> ResolveAsync(long cashRefundCents, CancellationToken cancellationToken)
    {
        if (_license?.IsModuleActive(LicensedModule.CashShifts) == false)
        {
            return Result.Success<CashShift?>(null);
        }

        var shift = await _shifts.GetOpenAsync(CashRegister.Default, cancellationToken);
        if (cashRefundCents <= 0)
        {
            return Result.Success(shift);
        }

        if (shift is null)
        {
            return Result.Failure<CashShift?>(new ShiftRequired());
        }

        if (shift.OpenedBy != _currentUser.UserId && !await _access.HasAsync(Permission.ManageShifts, cancellationToken))
        {
            return Result.Failure<CashShift?>(new ShiftOwnedByOther(await _guard.NameOfAsync(shift.OpenedBy, cancellationToken)));
        }

        var expected = shift.ExpectedCash(await _sales.GetShiftTotalsAsync(shift.Id, cancellationToken));
        return CashShiftMath.CanRefund(expected, cashRefundCents)
            ? Result.Success<CashShift?>(shift)
            : Result.Failure<CashShift?>(new InsufficientCash(null));
    }
}
