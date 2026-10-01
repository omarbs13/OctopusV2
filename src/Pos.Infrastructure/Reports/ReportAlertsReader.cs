using Microsoft.EntityFrameworkCore;
using Pos.Application.Reports;
using Pos.Application.Reports.GetReportAlerts;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Reports;

/// <summary>Productos críticos activos con control de inventario y su existencia actual (0 si no tienen movimientos).</summary>
internal sealed class ReportAlertsReader : IReportAlertsReader
{
    private readonly PosDbContext _context;

    public ReportAlertsReader(PosDbContext context) => _context = context;

    public async Task<IReadOnlyList<CriticalProductRow>> ListCriticalProductsAsync(CancellationToken cancellationToken)
    {
        var rows = await (
                from p in _context.Products.AsNoTracking()
                where p.DeletedAt == null && p.IsActive && p.TracksInventory && p.IsCritical
                join u in _context.UnitsOfMeasure on p.UnitCode equals u.Code
                join s in _context.ProductStocks on p.Id equals s.ProductId into stocks
                from s in stocks.DefaultIfEmpty()
                orderby p.NameSearch, p.Sku
                select new
                {
                    p.Id,
                    p.Name,
                    p.Sku,
                    OnHand = s == null ? 0L : s.OnHandThousandths,
                    Minimum = p.MinimumStockThousandths,
                    UnitName = u.Name,
                    u.DecimalPlaces,
                })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new CriticalProductRow(r.Id, r.Name, r.Sku, r.OnHand, r.Minimum, r.UnitName, r.DecimalPlaces))];
    }
}
