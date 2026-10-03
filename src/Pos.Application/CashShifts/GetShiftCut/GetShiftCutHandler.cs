using Pos.Application.Abstractions;
using Pos.Application.Licensing;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.CashShifts.GetShiftCut;

/// <summary>
/// Reporte fijo de un corte (FR-006, FR-014). Lo ve quien lo generó (el Cajero autorizado ve su
/// Corte X) o quien administra turnos.
/// </summary>
public sealed class GetShiftCutHandler
{
    private readonly IAccessControl _access;
    private readonly ILicenseState? _license;
    private readonly ICurrentUser _currentUser;
    private readonly ICashShiftRepository _shifts;

    public GetShiftCutHandler(IAccessControl access, ICurrentUser currentUser, ICashShiftRepository shifts, ILicenseState? license = null)
    {
        _license = license;
        _access = access;
        _currentUser = currentUser;
        _shifts = shifts;
    }

    public async Task<Result<ShiftCutReportDto>> HandleAsync(GetShiftCutQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // 025, SC-005: en bloqueo no se consultan ni reimprimen cortes.
        if (LicenseGate.WhenBlocked(_license) is { } blocked)
        {
            return Result.Failure<ShiftCutReportDto>(blocked);
        }

        var access = await _access.CheckAsync(Permission.OperateShift, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<ShiftCutReportDto>(access.Error!);
        }

        var report = await _shifts.GetCutReportAsync(query.CutId, cancellationToken);
        if (report is null)
        {
            return Result.Failure<ShiftCutReportDto>(new NotFound());
        }

        if (report.GeneratedById != _currentUser.UserId
            && await _access.CheckAsync(Permission.ManageShifts, cancellationToken) is { Allowed: false } denied)
        {
            return Result.Failure<ShiftCutReportDto>(denied.Error!);
        }

        return Result.Success(report);
    }
}
