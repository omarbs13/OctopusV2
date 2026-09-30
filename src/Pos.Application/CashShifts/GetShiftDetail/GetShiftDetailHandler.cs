using Pos.Application.Abstractions;
using Pos.Application.Sales;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.CashShifts.GetShiftDetail;

/// <summary>
/// Detalle de un turno: ventas, movimientos y arqueo (FR-021). El arqueo es la instantánea si está
/// cerrado; si está abierto, el esperado calculado al momento, visible solo para el administrador.
/// </summary>
public sealed class GetShiftDetailHandler
{
    private readonly IAccessControl _access;
    private readonly ICashShiftRepository _shifts;
    private readonly ISaleRepository _sales;

    public GetShiftDetailHandler(IAccessControl access, ICashShiftRepository shifts, ISaleRepository sales)
    {
        _access = access;
        _shifts = shifts;
        _sales = sales;
    }

    public async Task<Result<ShiftDetailDto>> HandleAsync(GetShiftDetailQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var access = await _access.CheckAsync(Permission.ManageShifts, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<ShiftDetailDto>(access.Error!);
        }

        var totals = await _sales.GetShiftTotalsAsync(query.ShiftId, cancellationToken);
        var detail = await _shifts.GetDetailAsync(query.ShiftId, totals, cancellationToken);
        if (detail is null)
        {
            return Result.Failure<ShiftDetailDto>(new NotFound());
        }

        var sales = await _sales.ListByShiftAsync(query.ShiftId, cancellationToken);
        return Result.Success(detail with { Sales = sales });
    }
}
