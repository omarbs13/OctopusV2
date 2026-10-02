using Pos.Application.Abstractions;
using Pos.Application.Reports;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.CashShifts.SearchShiftCuts;

/// <summary>Cortes X y Z del más reciente al más antiguo, paginados de 100 en 100 (FR-015).</summary>
public sealed class SearchShiftCutsHandler
{
    private readonly IAccessControl _access;
    private readonly ICashShiftRepository _shifts;
    private readonly ReportPeriodResolver _periods;

    public SearchShiftCutsHandler(IAccessControl access, ICashShiftRepository shifts, ReportPeriodResolver periods)
    {
        _access = access;
        _shifts = shifts;
        _periods = periods;
    }

    public async Task<Result<ShiftCutPage>> HandleAsync(SearchShiftCutsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var access = await _access.CheckAsync(Permission.ManageShifts, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<ShiftCutPage>(access.Error!);
        }

        if (query.From is { } from && query.To is { } to && from > to)
        {
            return Result.Failure<ShiftCutPage>(new ValidationFailed(
                [new FieldError(CashShiftFields.DateRange, CashShiftMessages.DateRangeInvalid)]));
        }

        var search = new ShiftCutSearch(
            query.Type,
            query.From is { } start ? _periods.StartOfDayUtc(start) : null,
            query.To is { } end ? _periods.StartOfDayUtc(end.AddDays(1)) : null,
            query.UserId,
            Math.Max(query.Page, 1),
            ShiftCutPage.DefaultPageSize);
        return Result.Success(await _shifts.SearchCutsAsync(search, cancellationToken));
    }
}
