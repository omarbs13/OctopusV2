using Pos.Application.Inventory;
using Pos.Domain.Inventory;

namespace Pos.Application.Reports.GetInventoryReport;

public enum InventoryReportSort
{
    Name,
    Sku,
    OnHand,
}

/// <summary>
/// Criterios del reporte de inventario al cierre de <c>AsOfDate</c> (fecha local). El filtro de estado y
/// la búsqueda por nombre o SKU solo afectan a la tabla, no a las tarjetas ni a la gráfica.
/// </summary>
public sealed record InventoryReportQuery(
    DateOnly AsOfDate,
    StockFilter Filter = StockFilter.All,
    string? SearchText = null,
    InventoryReportSort Sort = InventoryReportSort.Name,
    bool Descending = false,
    int Page = 1,
    int PageSize = InventoryReportQuery.DefaultPageSize)
{
    public const int DefaultPageSize = 100;
}

/// <summary>Conteos sobre todos los productos que controlan inventario (activos e inactivos).</summary>
public sealed record InventoryCounts(int Total, int Active, int Low, int Out, int Normal);

/// <summary>Producto con la existencia vigente a la fecha; nombre, SKU, unidad y mínimo son los actuales. Sin costos (FR-014).</summary>
public sealed record InventoryReportRow(
    Guid ProductId,
    string Name,
    string Sku,
    long OnHandThousandths,
    long? MinimumThousandths,
    string UnitName,
    int DecimalPlaces,
    StockStatus Status);

public sealed record InventoryReport(
    InventoryCounts Counts,
    IReadOnlyList<InventoryReportRow> Rows,
    long TotalRows,
    int Page,
    int PageSize)
{
    public int TotalPages => TotalRows <= 0 ? 1 : (int)((TotalRows + PageSize - 1) / PageSize);
}
