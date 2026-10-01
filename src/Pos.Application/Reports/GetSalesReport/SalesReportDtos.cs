using Pos.Application.Sales;
using Pos.Domain.Reports;

namespace Pos.Application.Reports.GetSalesReport;

public enum SalesReportSort
{
    Folio,
    Date,
    Cashier,
    Total,
}

/// <summary>Criterios del reporte de ventas; <c>PageSize</c> 100 en pantalla (research §2).</summary>
public sealed record SalesReportQuery(
    ReportPeriod Period,
    Guid? CashierId = null,
    bool Compare = false,
    SalesReportSort Sort = SalesReportSort.Date,
    bool Descending = true,
    int Page = 1,
    int PageSize = SalesReportQuery.DefaultPageSize)
{
    public const int DefaultPageSize = 100;
}

/// <summary>
/// Totales de ventas completadas; todo en centavos. <c>OnAccountCents</c> son las ventas a crédito (014),
/// incluidas en el total. <c>DiscountCents</c> es el total descontado del período (015, FR-018).
/// </summary>
public sealed record SalesTotals(
    int SalesCount,
    long TotalCents,
    long AverageTicketCents,
    long CashCents,
    long CardCents,
    long TransferCents,
    long CreditNoteCents = 0,
    long OnAccountCents = 0,
    long DiscountCents = 0)
{
    public static SalesTotals Empty { get; } = new(0, 0, 0, 0, 0, 0);
}

/// <summary>Período anterior y variación porcentual del total en puntos base; nula si el anterior es cero.</summary>
public sealed record SalesComparison(SalesTotals Previous, long? VariationBasisPoints);

public sealed record SalesReportRow(Guid SaleId, string FolioText, DateTime CreatedAtUtc, string CashierName, long TotalCents);

public sealed record SalesReport(
    SalesTotals Totals,
    SalesComparison? Comparison,
    IReadOnlyList<DayTotal> Days,
    IReadOnlyList<SalesReportRow> Rows,
    long TotalRows,
    int Page,
    int PageSize)
{
    public int TotalPages => TotalRows <= 0 ? 1 : (int)((TotalRows + PageSize - 1) / PageSize);
}
