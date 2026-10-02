using Pos.Application.Categories;
using Pos.Application.Inventory;
using Pos.Domain.Inventory;

namespace Pos.Application.Reports.GetInventoryReport;

public enum InventoryReportSort
{
    Name,
    Sku,
    OnHand,

    /// <summary>Por nombre de categoría, con "Sin categoría" al final; luego por nombre del producto (016, FR-020).</summary>
    Category,
}

/// <summary>
/// Criterios del reporte de inventario al cierre de <c>AsOfDate</c> (fecha local). El filtro de estado y
/// la búsqueda por nombre o SKU solo afectan a la tabla, no a las tarjetas ni a la gráfica. El filtro de
/// categoría sí afecta a tarjetas, gráfica y tabla: se aplica antes de contar (016, research §13).
/// </summary>
public sealed record InventoryReportQuery(
    DateOnly AsOfDate,
    StockFilter Filter = StockFilter.All,
    string? SearchText = null,
    InventoryReportSort Sort = InventoryReportSort.Name,
    bool Descending = false,
    int Page = 1,
    int PageSize = InventoryReportQuery.DefaultPageSize,
    CategoryFilter Category = default)
{
    public const int DefaultPageSize = 100;
}

/// <summary>Conteos sobre todos los productos que controlan inventario (activos e inactivos).</summary>
public sealed record InventoryCounts(int Total, int Active, int Low, int Out, int Normal);

/// <summary>
/// Producto con la existencia vigente a la fecha; nombre, SKU, unidad, mínimo y categoría son los actuales.
/// Sin costos (FR-014). <c>CategoryName</c> nulo = "Sin categoría" (016).
/// </summary>
public sealed record InventoryReportRow(
    Guid ProductId,
    string Name,
    string Sku,
    long OnHandThousandths,
    long? MinimumThousandths,
    string UnitName,
    int DecimalPlaces,
    StockStatus Status,
    string? CategoryName = null,
    bool CategoryIsActive = true);

public sealed record InventoryReport(
    InventoryCounts Counts,
    IReadOnlyList<InventoryReportRow> Rows,
    long TotalRows,
    int Page,
    int PageSize)
{
    public int TotalPages => TotalRows <= 0 ? 1 : (int)((TotalRows + PageSize - 1) / PageSize);
}
