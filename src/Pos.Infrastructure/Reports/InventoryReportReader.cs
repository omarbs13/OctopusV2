using Microsoft.EntityFrameworkCore;
using Pos.Application.Inventory;
using Pos.Application.Reports;
using Pos.Application.Reports.GetInventoryReport;
using Pos.Domain.Common;
using Pos.Domain.Inventory;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Reports;

/// <summary>
/// Existencias al cierre de una fecha (research §4): la existencia es el <c>ResultingStock</c> del último
/// movimiento anterior al límite (subconsulta apoyada en <c>IX_InventoryMovements_Product_CreatedAt</c>),
/// 0 si no hay. Las tarjetas, la tabla y la gráfica salen del mismo conjunto para que los números cuadren.
/// </summary>
internal sealed class InventoryReportReader : IInventoryReportReader
{
    private readonly PosDbContext _context;

    public InventoryReportReader(PosDbContext context) => _context = context;

    public async Task<InventoryReport> GetAsync(DateTime endUtcExclusive, InventoryReportQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var all = await (
                from p in _context.Products.AsNoTracking()
                where p.DeletedAt == null && p.TracksInventory && p.CreatedAt < endUtcExclusive
                join u in _context.UnitsOfMeasure on p.UnitCode equals u.Code
                select new
                {
                    p.Id,
                    p.Name,
                    p.NameSearch,
                    p.Sku,
                    p.IsActive,
                    Minimum = p.MinimumStockThousandths,
                    UnitName = u.Name,
                    u.DecimalPlaces,
                    OnHand = _context.InventoryMovements
                        .Where(m => m.ProductId == p.Id && m.CreatedAt < endUtcExclusive)
                        .OrderByDescending(m => m.Sequence)
                        .Select(m => (long?)m.ResultingStockThousandths)
                        .FirstOrDefault() ?? 0L,
                })
            .ToListAsync(cancellationToken);

        var items = all
            .Select(p => new
            {
                Product = p,
                Status = StockStatusRule.Evaluate(
                    StockLevel.FromThousandths(p.OnHand),
                    p.Minimum is { } m ? Quantity.FromThousandths(m) : null),
            })
            .ToList();

        var counts = new InventoryCounts(
            items.Count,
            items.Count(i => i.Product.IsActive),
            items.Count(i => i.Status == StockStatus.Low),
            items.Count(i => i.Status == StockStatus.Out),
            items.Count(i => i.Status == StockStatus.Normal));

        var filtered = items.Where(i => query.Filter switch
        {
            StockFilter.Normal => i.Status == StockStatus.Normal,
            StockFilter.Low => i.Status == StockStatus.Low,
            StockFilter.Out => i.Status == StockStatus.Out,
            _ => true,
        });

        if (query.SearchText is { Length: > 0 } text)
        {
            var name = TextNormalizer.ForSearch(text);
            filtered = filtered.Where(i =>
                i.Product.NameSearch.Contains(name, StringComparison.Ordinal)
                || i.Product.Sku.Contains(text, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = (query.Sort, query.Descending) switch
        {
            (InventoryReportSort.OnHand, false) => filtered.OrderBy(i => i.Product.OnHand).ThenBy(i => i.Product.NameSearch, StringComparer.Ordinal),
            (InventoryReportSort.OnHand, true) => filtered.OrderByDescending(i => i.Product.OnHand).ThenBy(i => i.Product.NameSearch, StringComparer.Ordinal),
            (InventoryReportSort.Sku, false) => filtered.OrderBy(i => i.Product.Sku, StringComparer.Ordinal),
            (InventoryReportSort.Sku, true) => filtered.OrderByDescending(i => i.Product.Sku, StringComparer.Ordinal),
            (_, false) => filtered.OrderBy(i => i.Product.NameSearch, StringComparer.Ordinal).ThenBy(i => i.Product.Sku, StringComparer.Ordinal),
            _ => filtered.OrderByDescending(i => i.Product.NameSearch, StringComparer.Ordinal).ThenBy(i => i.Product.Sku, StringComparer.Ordinal),
        };

        var list = ordered.ToList();
        var page = Math.Clamp(query.Page, 1, Math.Max(1, (int)Math.Ceiling(list.Count / (double)query.PageSize)));
        var rows = list
            .Skip((page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(i => new InventoryReportRow(
                i.Product.Id,
                i.Product.Name,
                i.Product.Sku,
                i.Product.OnHand,
                i.Product.Minimum,
                i.Product.UnitName,
                i.Product.DecimalPlaces,
                i.Status))
            .ToList();

        return new InventoryReport(counts, rows, list.Count, page, query.PageSize);
    }
}
