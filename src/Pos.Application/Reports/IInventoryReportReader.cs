using Pos.Application.Reports.GetInventoryReport;

namespace Pos.Application.Reports;

/// <summary>
/// Lector de solo lectura de existencias a una fecha pasada (research §4). <c>endUtcExclusive</c> es el
/// inicio del día siguiente al de <see cref="InventoryReportQuery.AsOfDate"/>, en UTC.
/// </summary>
public interface IInventoryReportReader
{
    Task<InventoryReport> GetAsync(DateTime endUtcExclusive, InventoryReportQuery query, CancellationToken cancellationToken);
}
