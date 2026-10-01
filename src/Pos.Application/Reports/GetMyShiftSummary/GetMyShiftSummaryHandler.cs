using Pos.Application.Abstractions;
using Pos.Application.CashShifts;
using Pos.Application.Sales;
using Pos.Application.Users.Access;
using Pos.Domain.CashShifts;
using Pos.Domain.Users;

namespace Pos.Application.Reports.GetMyShiftSummary;

/// <summary>
/// Resumen del turno del usuario conectado (<c>OperateShift</c>). Consulta siempre por el dueño del turno: un
/// id ajeno responde "no encontrado" y nunca revela que existe (FR-002, FR-020).
/// </summary>
public sealed class GetMyShiftSummaryHandler
{
    private readonly IAccessControl _access;
    private readonly ICurrentUser _currentUser;
    private readonly ICashShiftRepository _shifts;
    private readonly ISaleRepository _sales;

    public GetMyShiftSummaryHandler(IAccessControl access, ICurrentUser currentUser, ICashShiftRepository shifts, ISaleRepository sales)
    {
        _access = access;
        _currentUser = currentUser;
        _shifts = shifts;
        _sales = sales;
    }

    /// <summary>Con <paramref name="shiftId"/> nulo devuelve el turno abierto propio, si lo hay.</summary>
    public async Task<Result<MyShiftSummary>> HandleAsync(Guid? shiftId, CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(Permission.OperateShift, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<MyShiftSummary>(access.Error!);
        }

        var shift = shiftId is { } id
            ? await _shifts.GetAsync(id, cancellationToken)
            : await _shifts.GetOpenAsync(CashRegister.Default, cancellationToken);
        if (shift is null || shift.OpenedBy != _currentUser.UserId)
        {
            return Result.Failure<MyShiftSummary>(new NotFound());
        }

        var movements = shift.Movements
            .OrderBy(m => m.Sequence)
            .Select(m => new MyShiftMovement(m.CreatedAt, m.Type, m.AmountCents, m.Reason))
            .ToList();

        if (shift.Status == CashShiftStatus.Open)
        {
            var totals = await _sales.GetShiftTotalsAsync(shift.Id, cancellationToken);
            return Result.Success(new MyShiftSummary(
                shift.Id, shift.Folio, shift.OpenedAt, null, true, shift.OpeningFloatCents, totals.SalesCount, totals.TotalSoldCents,
                shift.DepositsTotalCents, shift.WithdrawalsTotalCents, null, null, null, movements));
        }

        return Result.Success(new MyShiftSummary(
            shift.Id, shift.Folio, shift.OpenedAt, shift.ClosedAt, false, shift.OpeningFloatCents, shift.SalesCount ?? 0, shift.TotalSoldCents ?? 0,
            shift.DepositsCents ?? 0, shift.WithdrawalsCents ?? 0, shift.ExpectedCashCents, shift.CountedCashCents, shift.DifferenceCents, movements));
    }
}
