using Pos.Application.Abstractions;
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
    private readonly ICurrentUser _currentUser;
    private readonly ICashShiftRepository _shifts;

    public GetShiftCutHandler(IAccessControl access, ICurrentUser currentUser, ICashShiftRepository shifts)
    {
        _access = access;
        _currentUser = currentUser;
        _shifts = shifts;
    }

    public async Task<Result<ShiftCutReportDto>> HandleAsync(GetShiftCutQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

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
