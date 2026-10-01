using Pos.Application.Abstractions;
using Pos.Application.Reports.GetCashCountReport;
using Pos.Application.Users.Access;
using Pos.Domain.Common;
using Pos.Domain.Inventory;
using Pos.Domain.Reports;
using Pos.Domain.Users;

namespace Pos.Application.Reports.GetReportAlerts;

/// <summary>
/// Alertas de Inicio para quien tiene <c>ViewReports</c>: diferencias de arqueo sobre el umbral en los
/// últimos 7 días y productos críticos con existencia baja o agotada (FR-021).
/// </summary>
public sealed class GetReportAlertsHandler
{
    public const int MaxItems = 10;
    private const int Days = 7;

    private readonly IAccessControl _access;
    private readonly ICashCountReportReader _cashCount;
    private readonly IReportAlertsReader _products;
    private readonly IReportSettingsStore _settings;
    private readonly ReportPeriodResolver _resolver;
    private readonly IClock _clock;

    public GetReportAlertsHandler(
        IAccessControl access,
        ICashCountReportReader cashCount,
        IReportAlertsReader products,
        IReportSettingsStore settings,
        ReportPeriodResolver resolver,
        IClock clock)
    {
        _access = access;
        _cashCount = cashCount;
        _products = products;
        _settings = settings;
        _resolver = resolver;
        _clock = clock;
    }

    public async Task<Result<ReportAlerts>> HandleAsync(CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(Permission.ViewReports, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<ReportAlerts>(access.Error!);
        }

        var today = _resolver.ToLocalDate(_clock.UtcNow);
        var window = _resolver.Resolve(ReportPeriod.Last7Days(today));
        var raw = await _cashCount.GetAsync(window, null, cancellationToken);
        var report = GetCashCountReportHandler.Build(raw, _settings.Load().CashDifferenceAlertBasisPoints);
        var cash = report.Rows
            .Where(r => r.IsAlert && r.DifferenceCents is not null)
            .OrderByDescending(r => r.OpenedAtUtc)
            .ToList();

        var critical = (await _products.ListCriticalProductsAsync(cancellationToken))
            .Select(p => new CriticalStockAlert(
                p.ProductId,
                p.Name,
                p.Sku,
                p.OnHandThousandths,
                p.MinimumThousandths,
                p.DecimalPlaces,
                p.UnitName,
                StockStatusRule.Evaluate(
                    StockLevel.FromThousandths(p.OnHandThousandths),
                    p.MinimumThousandths is { } minimum ? Quantity.FromThousandths(minimum) : null)))
            .Where(a => a.Status != StockStatus.Normal)
            .ToList();

        return Result.Success(new ReportAlerts(
            [.. cash.Take(MaxItems).Select(r => new CashAlert(r.ShiftId, r.FolioText, r.CashierName, r.OpenedAtUtc, r.DifferenceCents!.Value, r.DifferenceBasisPoints))],
            cash.Count,
            [.. critical.Take(MaxItems)],
            critical.Count));
    }
}
