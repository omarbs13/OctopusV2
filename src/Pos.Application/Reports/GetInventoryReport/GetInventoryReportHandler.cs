using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Reports.GetInventoryReport;

/// <summary>Inventario al cierre de una fecha; Administrador y Cajero (<c>ViewInventory</c>), en solo lectura y sin costos.</summary>
public sealed class GetInventoryReportHandler
{
    private const int MaxPageSize = ReportPaging.All;

    private readonly IAccessControl _access;
    private readonly IInventoryReportReader _reader;
    private readonly ReportPeriodResolver _resolver;

    public GetInventoryReportHandler(IAccessControl access, IInventoryReportReader reader, ReportPeriodResolver resolver)
    {
        _access = access;
        _reader = reader;
        _resolver = resolver;
    }

    public async Task<Result<InventoryReport>> HandleAsync(InventoryReportQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var access = await _access.CheckAsync(Permission.ViewInventory, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<InventoryReport>(access.Error!);
        }

        if (query.AsOfDate.Year < 2000)
        {
            return Result.Failure<InventoryReport>(
                new ValidationFailed([new FieldError(ReportFields.AsOfDate, ReportMessages.InvalidDate)]));
        }

        var normalized = query with
        {
            Page = Math.Max(query.Page, 1),
            PageSize = Math.Clamp(query.PageSize, 1, MaxPageSize),
            SearchText = string.IsNullOrWhiteSpace(query.SearchText) ? null : query.SearchText.Trim(),
        };
        var endUtcExclusive = _resolver.StartOfDayUtc(query.AsOfDate.AddDays(1));
        return Result.Success(await _reader.GetAsync(endUtcExclusive, normalized, cancellationToken));
    }
}
