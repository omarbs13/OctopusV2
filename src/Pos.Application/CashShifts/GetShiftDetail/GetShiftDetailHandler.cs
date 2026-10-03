using Pos.Application.Abstractions;
using Pos.Application.Licensing;
using Pos.Application.Sales;
using Pos.Application.Users.Access;
using Pos.Domain.CashShifts;
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
    private readonly ILicenseState? _license;

    public GetShiftDetailHandler(IAccessControl access, ICashShiftRepository shifts, ISaleRepository sales, ILicenseState? license = null)
    {
        _license = license;
        _access = access;
        _shifts = shifts;
        _sales = sales;
    }

    public async Task<Result<ShiftDetailDto>> HandleAsync(GetShiftDetailQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // 025, FR-030a: el turno abierto se consulta aunque Turnos y arqueo no esté activo o el sistema esté
        // bloqueado; los turnos cerrados siguen las reglas de licencia completas (blocked-mode §1).
        var access = await _access.CheckToFinishAsync(Permission.ManageShifts, cancellationToken);
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

        if (detail.Status != CashShiftStatus.Open)
        {
            if (LicenseGate.WhenBlocked(_license) is { } blocked)
            {
                return Result.Failure<ShiftDetailDto>(blocked);
            }

            var closed = await _access.CheckAsync(Permission.ManageShifts, cancellationToken);
            if (!closed.Allowed)
            {
                return Result.Failure<ShiftDetailDto>(closed.Error!);
            }
        }

        var sales = await _sales.ListByShiftAsync(query.ShiftId, cancellationToken);
        return Result.Success(detail with { Sales = sales });
    }
}
