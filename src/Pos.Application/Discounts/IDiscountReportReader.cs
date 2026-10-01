using Pos.Application.Reports;

namespace Pos.Application.Discounts;

/// <summary>Consulta del reporte de descuentos sobre las ventas completadas (015, research §12); solo lectura.</summary>
public interface IDiscountReportReader
{
    Task<DiscountReport> ReadAsync(DiscountReportQuery query, ReportWindow window, CancellationToken cancellationToken);
}
