using Pos.Application.Abstractions;
using Pos.Application.Licensing;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.CashShifts.SearchShifts;

/// <summary>Turnos de la apertura más reciente a la más antigua, paginados de 100 en 100 (FR-020).</summary>
public sealed class SearchShiftsHandler
{
    private readonly IAccessControl _access;
    private readonly ILicenseState? _license;
    private readonly ICashShiftRepository _shifts;

    public SearchShiftsHandler(IAccessControl access, ICashShiftRepository shifts, ILicenseState? license = null)
    {
        _license = license;
        _access = access;
        _shifts = shifts;
    }

    public async Task<Result<ShiftPage>> HandleAsync(SearchShiftsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // 025, SC-005: en bloqueo no se consultan turnos anteriores.
        if (LicenseGate.WhenBlocked(_license) is { } blocked)
        {
            return Result.Failure<ShiftPage>(blocked);
        }

        var access = await _access.CheckAsync(Permission.ManageShifts, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<ShiftPage>(access.Error!);
        }

        if (query.FromUtc is { } from && query.ToUtcExclusive is { } to && from > to)
        {
            return Result.Failure<ShiftPage>(new ValidationFailed(
                [new FieldError(CashShiftFields.DateRange, CashShiftMessages.DateRangeInvalid)]));
        }

        var search = new ShiftSearch(query.FromUtc, query.ToUtcExclusive, query.UserId, query.Status, Math.Max(query.Page, 1), ShiftPage.DefaultPageSize);
        return Result.Success(await _shifts.SearchAsync(search, cancellationToken));
    }
}
