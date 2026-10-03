using Pos.Application.Abstractions;
using Pos.Application.Sales;
using Pos.Application.Users.Access;
using Pos.Domain.CashShifts;
using Pos.Domain.Users;

namespace Pos.Application.CashShifts.GetCurrentShift;

/// <summary>
/// Resumen del turno abierto de la caja para el Punto de venta, Inicio y el cajero (Historias 1, 5 y 6).
/// Nunca devuelve fondo, esperado, desglose por forma de pago ni movimientos (FR-022); <c>null</c> si
/// no hay turno abierto.
/// </summary>
public sealed class GetCurrentShiftHandler
{
    private readonly IAccessControl _access;
    private readonly ICurrentUser _currentUser;
    private readonly ICashShiftRepository _shifts;
    private readonly ISaleRepository _sales;
    private readonly ShiftGuard _guard;

    public GetCurrentShiftHandler(
        IAccessControl access,
        ICurrentUser currentUser,
        ICashShiftRepository shifts,
        ISaleRepository sales,
        ShiftGuard guard)
    {
        _access = access;
        _currentUser = currentUser;
        _shifts = shifts;
        _sales = sales;
        _guard = guard;
    }

    public async Task<Result<CurrentShiftSummary?>> HandleAsync(CancellationToken cancellationToken)
    {
        // 025, FR-030a: el turno abierto se consulta, cuenta y cierra aunque Turnos y arqueo no esté activo o el
        // sistema esté bloqueado (Principio I); solo se exigen sesión y rol.
        var access = await _access.CheckToFinishAsync(Permission.OperateShift, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<CurrentShiftSummary?>(access.Error!);
        }

        var shift = await _shifts.GetOpenAsync(CashRegister.Default, cancellationToken);
        if (shift is null)
        {
            return Result.Success<CurrentShiftSummary?>(null);
        }

        var totals = await _sales.GetShiftTotalsAsync(shift.Id, cancellationToken);
        return Result.Success<CurrentShiftSummary?>(new CurrentShiftSummary(
            shift.Id,
            shift.Version,
            shift.Folio,
            shift.OpenedAt,
            shift.OpenedBy,
            await _guard.NameOfAsync(shift.OpenedBy, cancellationToken),
            shift.OpenedBy == _currentUser.UserId,
            totals.SalesCount,
            totals.TotalSoldCents));
    }
}
