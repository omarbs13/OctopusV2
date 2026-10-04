using Pos.Application.Abstractions;
using Pos.Application.Sales;
using Pos.Application.Users;
using Pos.Domain.CashShifts;

namespace Pos.Application.CashShifts;

/// <summary>
/// Reglas de turno compartidas por los casos de uso que corren dentro de la transacción de escritura:
/// turno abierto propio (FR-001, FR-005) y venta en curso del dueño (FR-013).
/// </summary>
public sealed class ShiftGuard
{
    private readonly ICashShiftRepository _shifts;
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _users;
    private readonly ISaleDraftStore _drafts;

    public ShiftGuard(ICashShiftRepository shifts, ICurrentUser currentUser, IUserRepository users, ISaleDraftStore drafts)
    {
        _shifts = shifts;
        _currentUser = currentUser;
        _users = users;
        _drafts = drafts;
    }

    /// <summary>
    /// El turno abierto de la caja si es del usuario actual; <see cref="ShiftRequired"/> si no hay
    /// ninguno y <see cref="ShiftOwnedByOther"/> si es de otro usuario.
    /// </summary>
    public async Task<Result<CashShift>> RequireOwnOpenShiftAsync(CancellationToken cancellationToken)
    {
        var shift = await _shifts.GetOpenAsync(CashRegister.Default, cancellationToken);
        if (shift is null)
        {
            return Result.Failure<CashShift>(new ShiftRequired());
        }

        return shift.OpenedBy == _currentUser.UserId
            ? Result.Success(shift)
            : Result.Failure<CashShift>(new ShiftOwnedByOther(await NameOfAsync(shift.OpenedBy, cancellationToken)));
    }

    /// <summary>
    /// Nulo si el turno se puede cerrar con respecto a la venta en curso de su dueño. Si el dueño es
    /// quien cierra y tiene venta en curso: <see cref="SaleInProgress"/>. Si un administrador cierra
    /// un turno ajeno con venta conservada: <see cref="HeldSaleWillBeDiscarded"/> hasta que confirme.
    /// </summary>
    public async Task<Error?> CheckHeldSaleAsync(CashShift shift, bool discardConfirmed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(shift);
        if (!await _drafts.HasForAsync(shift.OpenedBy, cancellationToken))
        {
            return null;
        }

        if (shift.OpenedBy == _currentUser.UserId)
        {
            return new SaleInProgress();
        }

        return discardConfirmed
            ? null
            : new HeldSaleWillBeDiscarded(await NameOfAsync(shift.OpenedBy, cancellationToken));
    }

    public async Task<string> NameOfAsync(Guid userId, CancellationToken cancellationToken) =>
        (await _users.GetAsync(userId, cancellationToken))?.UserName ?? SystemUser.NameOf(userId);
}
