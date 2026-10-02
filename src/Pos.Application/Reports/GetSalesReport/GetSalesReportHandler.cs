using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Reports;
using Pos.Domain.Users;

namespace Pos.Application.Reports.GetSalesReport;

/// <summary>Reporte de ventas por período; solo quien tiene <c>ViewReports</c> (FR-002, FR-026).</summary>
public sealed class GetSalesReportHandler
{
    private const int MaxPageSize = ReportPaging.All;

    private readonly IAccessControl _access;
    private readonly ISalesReportReader _reader;
    private readonly ReportPeriodResolver _resolver;

    public GetSalesReportHandler(IAccessControl access, ISalesReportReader reader, ReportPeriodResolver resolver)
    {
        _access = access;
        _reader = reader;
        _resolver = resolver;
    }

    public async Task<Result<SalesReport>> HandleAsync(SalesReportQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var access = await _access.CheckAsync(Permission.ViewReports, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<SalesReport>(access.Error!);
        }

        var normalized = query with
        {
            Page = Math.Max(query.Page, 1),
            PageSize = Math.Clamp(query.PageSize, 1, MaxPageSize),
        };
        var window = new SalesReportWindow(
            _resolver.Resolve(normalized.Period),
            _resolver.Days(normalized.Period),
            normalized.Compare ? _resolver.Resolve(normalized.Period.Previous()) : null);

        var report = await _reader.GetAsync(window, normalized, cancellationToken);

        // Porcentaje del total de cada categoría (016, research §10); la interfaz no lo calcula.
        var categoriesTotal = report.Categories.Sum(c => c.AmountCents);
        report = report with
        {
            Categories = [.. report.Categories.Select(c => c with { ShareBasisPoints = ShareMath.BasisPoints(c.AmountCents, categoriesTotal) })],
        };
        if (report.Comparison is { } comparison)
        {
            report = report with
            {
                Comparison = comparison with
                {
                    VariationBasisPoints = VariationMath.PercentBasisPoints(comparison.Previous.TotalCents, report.Totals.TotalCents),
                },
            };
        }

        return Result.Success(report);
    }
}
