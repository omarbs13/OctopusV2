using Pos.Application.Abstractions;
using Pos.Application.CashShifts;
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
    private readonly ICurrentUser _currentUser;
    private readonly ICashShiftRepository _shifts;

    public ListMyShiftsHandler(IAccessControl access, ICurrentUser currentUser, ICashShiftRepository shifts)
    {
        _access = access;
        _currentUser = currentUser;
        _shifts = shifts;
    }

    public async Task<Result<IReadOnlyList<MyShiftListItem>>> HandleAsync(CancellationToken cancellationToken)
    {
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
