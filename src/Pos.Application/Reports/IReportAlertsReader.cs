using Pos.Application.Reports.GetReportAlerts;

namespace Pos.Application.Reports;

/// <summary>Lector de solo lectura de productos críticos con su existencia actual (research §10).</summary>
public interface IReportAlertsReader
{
    /// <summary>Productos críticos, activos, no borrados y que controlan inventario, ordenados por nombre.</summary>
    Task<IReadOnlyList<CriticalProductRow>> ListCriticalProductsAsync(CancellationToken cancellationToken);
}
