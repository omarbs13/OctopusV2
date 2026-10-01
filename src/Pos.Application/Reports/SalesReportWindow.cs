using Pos.Application.Sales;

namespace Pos.Application.Reports;

/// <summary>
/// Ventanas UTC de un reporte de ventas: la del período, un elemento por día local (para agrupar la
/// gráfica sin conocer la zona horaria) y, solo con el comparativo activo, la del período anterior.
/// </summary>
public sealed record SalesReportWindow(ReportWindow Current, IReadOnlyList<DayWindow> Days, ReportWindow? Previous);
