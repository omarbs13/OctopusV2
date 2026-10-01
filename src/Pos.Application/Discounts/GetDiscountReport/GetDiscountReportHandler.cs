using Pos.Application.Abstractions;
using Pos.Application.Reports;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Discounts.GetDiscountReport;

/// <summary>
/// Reporte de descuentos del período (015, FR-018): total descontado y detalle por venta, filtrable por
/// cajero y tipo; solo ventas completadas. Requiere <c>ViewDiscountReport</c> (módulo Descuentos).
/// </summary>
public sealed class GetDiscountReportHandler
{
    private readonly IAccessControl _access;
    private readonly IDiscountReportReader _reader;
    private readonly ReportPeriodResolver _resolver;

    public GetDiscountReportHandler(IAccessControl access, IDiscountReportReader reader, ReportPeriodResolver resolver)
    {
        _access = access;
        _reader = reader;
        _resolver = resolver;
    }

    public async Task<Result<DiscountReport>> HandleAsync(DiscountReportQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var access = await _access.CheckAsync(Permission.ViewDiscountReport, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<DiscountReport>(access.Error!);
        }

        var normalized = query with
        {
            Page = Math.Max(1, query.Page),
            PageSize = Math.Clamp(query.PageSize, 1, ReportPaging.All),
        };
        return Result.Success(await _reader.ReadAsync(normalized, _resolver.Resolve(query.Period), cancellationToken));
    }
}
