using Pos.Application.Categories;
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

/// <summary>
/// Criterios del reporte de ventas; <c>PageSize</c> 100 en pantalla (research §2). <c>Category</c> limita
/// métricas, gráfica, detalle y "Ventas por categoría" a las líneas de esa categoría (016, FR-014).
/// </summary>
public sealed record SalesReportQuery(
    ReportPeriod Period,
    Guid? CashierId = null,
    bool Compare = false,
    SalesReportSort Sort = SalesReportSort.Date,
    bool Descending = true,
    int Page = 1,
    int PageSize = SalesReportQuery.DefaultPageSize,
    CategoryFilter Category = default)
{
    public const int DefaultPageSize = 100;
}

/// <summary>
/// Totales de ventas completadas; todo en centavos. <c>OnAccountCents</c> son las ventas a crédito (014),
/// incluidas en el total. <c>DiscountCents</c> es el total descontado del período (015, FR-018).
/// Con filtro de categoría las formas de pago no se desglosan (un pago cubre la venta completa): vienen en
/// 0 con <c>PaymentsBreakdownAvailable</c> falso (016, research §11).
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
    long DiscountCents = 0,
    bool PaymentsBreakdownAvailable = true)
{
    public static SalesTotals Empty { get; } = new(0, 0, 0, 0, 0, 0);
}

/// <summary>Período anterior y variación porcentual del total en puntos base; nula si el anterior es cero.</summary>
public sealed record SalesComparison(SalesTotals Previous, long? VariationBasisPoints);

public sealed record SalesReportRow(Guid SaleId, string FolioText, DateTime CreatedAtUtc, string CashierName, long TotalCents);

/// <summary>
/// Producto de una categoría en "Ventas por categoría" (016, FR-017): unidades e importe netos de
/// devoluciones; el importe ya es neto de descuentos (015). Nombre actual, o el último vendido si se borró.
/// </summary>
public sealed record ProductSales(
    Guid ProductId,
    string Name,
    string Sku,
    string UnitName,
    int DecimalPlaces,
    long UnitsThousandths,
    long AmountCents);

/// <summary>
/// Categoría vigente con ventas en el período (016, FR-016): <c>CategoryId</c> nulo = "Sin categoría".
/// <c>AmountCents</c> = Σ de sus productos; la suma de todas las categorías es el total sin filtro (FR-018).
/// <c>UnitsThousandths</c> suma cantidades de unidades distintas tal cual (research §9).
/// </summary>
public sealed record CategorySales(
    Guid? CategoryId,
    string Name,
    bool IsActive,
    long UnitsThousandths,
    long AmountCents,
    long ShareBasisPoints,
    IReadOnlyList<ProductSales> Products);

public sealed record SalesReport(
    SalesTotals Totals,
    SalesComparison? Comparison,
    IReadOnlyList<DayTotal> Days,
    IReadOnlyList<SalesReportRow> Rows,
    long TotalRows,
    int Page,
    int PageSize,
    IReadOnlyList<CategorySales> Categories)
{
    public int TotalPages => TotalRows <= 0 ? 1 : (int)((TotalRows + PageSize - 1) / PageSize);

    /// <summary>Fila de total de "Ventas por categoría": Σ unidades de las categorías.</summary>
    public long CategoriesUnitsThousandths => Categories.Sum(c => c.UnitsThousandths);

    /// <summary>Fila de total de "Ventas por categoría": Σ importes; sin filtro es igual al total vendido (FR-018).</summary>
    public long CategoriesAmountCents => Categories.Sum(c => c.AmountCents);
}
