using Pos.Application.Abstractions;
using Pos.Application.CashShifts;
using Pos.Application.Licensing;
using Pos.Application.Reports.GetMyShiftSummary;
using Pos.Application.Users.Access;
using Pos.Domain.CashShifts;
using Pos.Domain.Users;

namespace Pos.Application.Reports.ListMyShifts;

/// <summary>Los 10 turnos más recientes del usuario conectado (<c>OperateShift</c>); nunca turnos ajenos.</summary>
public sealed class ListMyShiftsHandler
{
    public const int Count = 10;

    private readonly IAccessControl _access;
    private readonly ILicenseState? _license;
    private readonly ICurrentUser _currentUser;
    private readonly ICashShiftRepository _shifts;

    public ListMyShiftsHandler(IAccessControl access, ICurrentUser currentUser, ICashShiftRepository shifts, ILicenseState? license = null)
    {
        _license = license;
        _access = access;
        _currentUser = currentUser;
        _shifts = shifts;
    }

    public async Task<Result<IReadOnlyList<MyShiftListItem>>> HandleAsync(CancellationToken cancellationToken)
    {
        // 025, SC-005: en bloqueo no se consultan turnos anteriores.
        if (LicenseGate.WhenBlocked(_license) is { } blocked)
        {
            return Result.Failure<IReadOnlyList<MyShiftListItem>>(blocked);
        }

        var access = await _access.CheckAsync(Permission.OperateShift, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<IReadOnlyList<MyShiftListItem>>(access.Error!);
        }

        var page = await _shifts.SearchAsync(new ShiftSearch(null, null, _currentUser.UserId, null, 1, Count), cancellationToken);
        IReadOnlyList<MyShiftListItem> items =
        [
            .. page.Items.Select(i => new MyShiftListItem(
                i.Id, i.Folio, i.OpenedAtUtc, i.ClosedAtUtc, i.Status == CashShiftStatus.Open, i.TotalSoldCents)),
        ];
        return Result.Success(items);
    }
}
