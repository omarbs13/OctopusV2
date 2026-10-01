using Pos.Application.Reports.GetSalesReport;

namespace Pos.Application.Reports;

/// <summary>
/// Lector de solo lectura del reporte de ventas (research §1): sumas en SQL sobre ventas completadas.
/// Devuelve <c>Comparison</c> con los totales anteriores y sin variación; la calcula el caso de uso.
/// </summary>
public interface ISalesReportReader
{
    Task<SalesReport> GetAsync(SalesReportWindow window, SalesReportQuery query, CancellationToken cancellationToken);
}
