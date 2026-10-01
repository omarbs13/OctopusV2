using Pos.Application.Discounts;
using Pos.Application.Reports.GetCashCountReport;
using Pos.Application.Reports.GetInventoryReport;
using Pos.Application.Reports.GetSalesReport;

namespace Pos.Application.Reports.Export;

public enum ReportKind
{
    Sales,
    CashCount,
    Inventory,
    MyShift,

    /// <summary>Reporte de descuentos (015, FR-018).</summary>
    Discounts,
}

public enum ExportFormat
{
    Pdf,
    Xlsx,
}

/// <summary>
/// Reporte y formato que se exportan, con los mismos parámetros que la pantalla (sin paginar: la
/// exportación incluye todos los registros del filtro, FR-018). Solo se llena el parámetro del reporte.
/// </summary>
public sealed record ExportRequest
{
    public required ReportKind Kind { get; init; }

    public required ExportFormat Format { get; init; }

    public SalesReportQuery? Sales { get; init; }

    public CashCountReportQuery? CashCount { get; init; }

    public InventoryReportQuery? Inventory { get; init; }

    public DiscountReportQuery? Discounts { get; init; }

    /// <summary>Turno propio de "Mi turno"; nulo para el turno abierto.</summary>
    public Guid? ShiftId { get; init; }

    public static ExportRequest ForSales(SalesReportQuery query, ExportFormat format) =>
        new() { Kind = ReportKind.Sales, Format = format, Sales = query };

    public static ExportRequest ForCashCount(CashCountReportQuery query, ExportFormat format) =>
        new() { Kind = ReportKind.CashCount, Format = format, CashCount = query };

    public static ExportRequest ForInventory(InventoryReportQuery query, ExportFormat format) =>
        new() { Kind = ReportKind.Inventory, Format = format, Inventory = query };

    public static ExportRequest ForDiscounts(DiscountReportQuery query, ExportFormat format) =>
        new() { Kind = ReportKind.Discounts, Format = format, Discounts = query };

    public static ExportRequest ForMyShift(Guid? shiftId) =>
        new() { Kind = ReportKind.MyShift, Format = ExportFormat.Pdf, ShiftId = shiftId };
}

/// <summary>Archivo generado; <c>BusinessMissing</c> indica que faltan los datos del negocio del encabezado.</summary>
public sealed record ExportedFile(string FileName, string ContentType, byte[] Bytes, bool BusinessMissing);
