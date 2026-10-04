using Microsoft.EntityFrameworkCore;
using Pos.Application.Categories;
using Pos.Application.Reports;
using Pos.Application.Reports.GetSalesReport;
using Pos.Application.Sales;
using Pos.Domain.Sales;
using Pos.Infrastructure.Categories;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Reports;

/// <summary>
/// Reporte de ventas con consultas agregadas sin seguimiento de cambios (research §1, §2). Solo cuenta
/// ventas completadas; los pagos son netos de cambio, así que efectivo + tarjeta + transferencia = total.
/// Con filtro de categoría (016, research §11) las cifras salen de las líneas de esa categoría; la sección
/// "Ventas por categoría" (research §12) agrupa siempre por la categoría vigente del producto.
/// </summary>
internal sealed class SalesReportReader : ISalesReportReader
{
    private readonly PosDbContext _context;

    public SalesReportReader(PosDbContext context) => _context = context;

    public async Task<SalesReport> GetAsync(SalesReportWindow window, SalesReportQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(query);

        var sales = Completed(window.Current, query.CashierId);
        var facts = await LineFactsAsync(sales, query.Category, cancellationToken);
        var categories = await CategoriesAsync(facts, cancellationToken);
        if (!query.Category.IsAll)
        {
            return await FilteredAsync(window, query, sales, facts, categories, cancellationToken);
        }

        var totals = await TotalsAsync(sales, cancellationToken);
        var comparison = window.Previous is { } previous
            ? new SalesComparison(await TotalsAsync(Completed(previous, query.CashierId), cancellationToken), null)
            : null;

        var days = await DaysAsync(sales, window.Days, cancellationToken);
        var (rows, totalRows) = await RowsAsync(sales, query, cancellationToken);
        return new SalesReport(totals, comparison, days, rows, totalRows, query.Page, query.PageSize, categories);
    }

    /// <summary>
    /// Reporte filtrado por categoría (research §11): ventas con al menos una línea de la categoría; total,
    /// gráfica y comparativo con el importe neto de esas líneas; detalle con su importe vendido (sin restar
    /// devoluciones, como la fila sin filtro); formas de pago sin desglose.
    /// </summary>
    private async Task<SalesReport> FilteredAsync(
        SalesReportWindow window,
        SalesReportQuery query,
        IQueryable<Sale> sales,
        IReadOnlyList<LineFact> facts,
        IReadOnlyList<CategorySales> categories,
        CancellationToken cancellationToken)
    {
        var totals = FilteredTotals(facts);
        var comparison = window.Previous is { } previous
            ? new SalesComparison(FilteredTotals(await LineFactsAsync(Completed(previous, query.CashierId), query.Category, cancellationToken)), null)
            : null;

        var totalsByDay = new long[window.Days.Count];
        var salesByDay = new HashSet<Guid>[window.Days.Count];
        foreach (var fact in facts)
        {
            var index = IndexOfDay(window.Days, fact.CreatedAt);
            if (index >= 0)
            {
                totalsByDay[index] += fact.NetCents;
                (salesByDay[index] ??= []).Add(fact.SaleId);
            }
        }

        var days = window.Days.Select((day, i) => new DayTotal(day.LocalDate, totalsByDay[i], salesByDay[i]?.Count ?? 0)).ToList();

        // Encabezados de las ventas con líneas de la categoría; se ordenan y paginan en memoria (≤ 10,000 filas).
        var products = _context.Products.AsNoTracking().WhereCategory(query.Category);
        var headers = await (
                from s in sales
                where _context.SaleLines.Any(l => l.SaleId == s.Id && products.Any(p => p.Id == l.ProductId))
                join u in _context.Users.AsNoTracking() on s.CreatedBy equals u.Id
                select new { s.Id, s.FolioNumber, s.CreatedAt, CashierName = u.UserName })
            .ToListAsync(cancellationToken);
        var amounts = facts.GroupBy(f => f.SaleId).ToDictionary(g => g.Key, g => g.Sum(f => f.AmountCents));
        var rows = headers.Select(h => new { h.Id, h.FolioNumber, h.CreatedAt, h.CashierName, TotalCents = amounts.GetValueOrDefault(h.Id) });

        var ordered = (query.Sort, query.Descending) switch
        {
            (SalesReportSort.Folio, false) => rows.OrderBy(r => r.FolioNumber),
            (SalesReportSort.Folio, true) => rows.OrderByDescending(r => r.FolioNumber),
            (SalesReportSort.Cashier, false) => rows.OrderBy(r => r.CashierName, StringComparer.Ordinal).ThenBy(r => r.FolioNumber),
            (SalesReportSort.Cashier, true) => rows.OrderByDescending(r => r.CashierName, StringComparer.Ordinal).ThenByDescending(r => r.FolioNumber),
            (SalesReportSort.Total, false) => rows.OrderBy(r => r.TotalCents).ThenBy(r => r.FolioNumber),
            (SalesReportSort.Total, true) => rows.OrderByDescending(r => r.TotalCents).ThenByDescending(r => r.FolioNumber),
            (_, false) => rows.OrderBy(r => r.CreatedAt).ThenBy(r => r.FolioNumber),
            _ => rows.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.FolioNumber),
        };

        var skip = (long)(query.Page - 1) * query.PageSize;
        var page = ordered
            .Skip((int)Math.Min(skip, int.MaxValue))
            .Take(query.PageSize)
            .Select(r => new SalesReportRow(r.Id, Folio.Format(r.FolioNumber), r.CreatedAt, r.CashierName, r.TotalCents))
            .ToList();
        return new SalesReport(totals, comparison, days, page, headers.Count, query.Page, query.PageSize, categories);
    }

    private static SalesTotals FilteredTotals(IReadOnlyList<LineFact> facts)
    {
        var count = facts.Select(f => f.SaleId).Distinct().Count();
        if (count == 0)
        {
            return SalesTotals.Empty with { PaymentsBreakdownAvailable = false };
        }

        var total = facts.Sum(f => f.NetCents);
        var average = (total + (count / 2)) / count;
        return new SalesTotals(count, total, average, 0, 0, 0, DiscountCents: facts.Sum(f => f.DiscountCents), PaymentsBreakdownAvailable: false);
    }

    /// <summary>
    /// Una fila por línea de las ventas consideradas cuyo producto cumple el filtro, con su categoría
    /// vigente e importe y unidades netos de devoluciones (research §9). Con 10,000 ventas son a lo más
    /// unas decenas de miles de filas de pocas columnas.
    /// </summary>
    private async Task<IReadOnlyList<LineFact>> LineFactsAsync(IQueryable<Sale> sales, CategoryFilter filter, CancellationToken cancellationToken)
    {
        var returned =
            from r in _context.SaleReturnLines.AsNoTracking()
            group r by r.SaleLineId into g
            select new { SaleLineId = g.Key, Cents = g.Sum(x => x.AmountCents) };

        var rows = await (
                from l in _context.SaleLines.AsNoTracking()
                join s in sales on l.SaleId equals s.Id
                join p in _context.Products.AsNoTracking().WhereCategory(filter) on l.ProductId equals p.Id
                join r in returned on l.Id equals r.SaleLineId into rs
                from r in rs.DefaultIfEmpty()
                select new
                {
                    l.SaleId,
                    l.ProductId,
                    p.CategoryId,
                    s.CreatedAt,
                    Units = l.QuantityThousandths - l.ReturnedQuantity,
                    l.AmountCents,
                    Returned = (long?)r.Cents ?? 0,
                    Discount = l.OriginalAmountCents - l.AmountCents,
                })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new LineFact(r.SaleId, r.ProductId, r.CategoryId, r.CreatedAt, r.Units, r.AmountCents, r.AmountCents - r.Returned, r.Discount))];
    }

    /// <summary>
    /// "Ventas por categoría" (research §12): agrupa por producto y luego por su categoría vigente;
    /// descarta lo que quedó en 0 por devoluciones; categorías por importe y productos por unidades.
    /// </summary>
    private async Task<IReadOnlyList<CategorySales>> CategoriesAsync(IReadOnlyList<LineFact> facts, CancellationToken cancellationToken)
    {
        var byProduct = facts
            .GroupBy(f => f.ProductId)
            .Select(g => new { ProductId = g.Key, g.First().CategoryId, Units = g.Sum(f => f.UnitsThousandths), Amount = g.Sum(f => f.NetCents) })
            .Where(p => p.Units != 0 || p.Amount != 0)
            .ToList();
        if (byProduct.Count == 0)
        {
            return [];
        }

        var productIds = byProduct.Select(p => p.ProductId).ToList();
        var products = await (
                from p in _context.Products.AsNoTracking()
                where productIds.Contains(p.Id)
                join u in _context.UnitsOfMeasure.AsNoTracking() on p.UnitCode equals u.Code
                select new { p.Id, p.Name, p.Sku, p.DeletedAt, UnitName = u.Name, u.DecimalPlaces })
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        // Producto borrado: se muestra con el último nombre con que se vendió.
        var deletedIds = products.Values.Where(p => p.DeletedAt != null).Select(p => p.Id).ToList();
        var lastNames = deletedIds.Count == 0
            ? []
            : (await (
                    from l in _context.SaleLines.AsNoTracking()
                    where deletedIds.Contains(l.ProductId)
                    join s in _context.Sales.AsNoTracking() on l.SaleId equals s.Id
                    select new { l.ProductId, l.ProductName, s.CreatedAt })
                .ToListAsync(cancellationToken))
                .GroupBy(l => l.ProductId)
                .ToDictionary(g => g.Key, g => g.MaxBy(l => l.CreatedAt)!.ProductName);

        var categoryIds = byProduct.Where(p => p.CategoryId != null).Select(p => p.CategoryId!.Value).Distinct().ToList();
        var categories = await _context.Categories.AsNoTracking()
            .Where(c => categoryIds.Contains(c.Id) && c.DeletedAt == null)
            .Select(c => new { c.Id, c.Name, c.IsActive })
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        return [.. byProduct
            .GroupBy(p => p.CategoryId is { } id && categories.ContainsKey(id) ? p.CategoryId : null)
            .Select(g =>
            {
                var items = g
                    .Select(p =>
                    {
                        var product = products[p.ProductId];
                        var name = product.DeletedAt != null && lastNames.TryGetValue(p.ProductId, out var last) ? last : product.Name;
                        return new ProductSales(p.ProductId, name, product.Sku, product.UnitName, product.DecimalPlaces, p.Units, p.Amount);
                    })
                    .OrderByDescending(p => p.UnitsThousandths)
                    .ThenByDescending(p => p.AmountCents)
                    .ThenBy(p => p.Name, StringComparer.CurrentCulture)
                    .ToList();
                var category = g.Key is { } id ? categories[id] : null;
                return new CategorySales(
                    g.Key,
                    category?.Name ?? CategoryMessages.Uncategorized,
                    category?.IsActive ?? true,
                    items.Sum(p => p.UnitsThousandths),
                    items.Sum(p => p.AmountCents),
                    0,
                    items);
            })
            .OrderByDescending(c => c.AmountCents)
            .ThenBy(c => c.Name, StringComparer.CurrentCulture)];
    }

    private IQueryable<Sale> Completed(ReportWindow window, Guid? cashierId)
    {
        var sales = _context.Sales.AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed && s.CreatedAt >= window.FromUtc && s.CreatedAt < window.ToUtcExclusive);
        return cashierId is { } id ? sales.Where(s => s.CreatedBy == id) : sales;
    }

    private async Task<SalesTotals> TotalsAsync(IQueryable<Sale> sales, CancellationToken cancellationToken)
    {
        var count = await sales.CountAsync(cancellationToken);
        if (count == 0)
        {
            return SalesTotals.Empty;
        }

        // Total neto de devoluciones parciales (013, research §11); la fila de cada venta conserva su importe original.
        var total = await sales.SumAsync(s => s.TotalCents - s.ReturnedCents, cancellationToken);
        var byMethod = await (
                from p in _context.SalePayments.AsNoTracking()
                join s in sales on p.SaleId equals s.Id
                group p by p.Method into g
                select new { Method = g.Key, Cents = g.Sum(x => x.AmountCents) })
            .ToListAsync(cancellationToken);

        // Lo cobrado por forma de pago menos lo reintegrado por esa misma forma (013). Lo devuelto con
        // nota de crédito no sale de caja: reduce el total pero no estas columnas.
        var refunded = await (
                from f in _context.SaleReturnRefunds.AsNoTracking()
                join r in _context.SaleReturns.AsNoTracking() on f.SaleReturnId equals r.Id
                join s in sales on r.SaleId equals s.Id
                group f by f.Method into g
                select new { Method = g.Key, Cents = g.Sum(x => x.AmountCents) })
            .ToListAsync(cancellationToken);

        long Paid(PaymentMethod method) =>
            byMethod.Where(m => m.Method == method).Sum(m => m.Cents) - refunded.Where(m => m.Method == method).Sum(m => m.Cents);

        // 015: total descontado de las ventas completadas; no depende de la licencia del módulo Descuentos (FR-023).
        var discounted = await sales.SumAsync(s => s.DiscountCents, cancellationToken);

        // Promedio: total / ventas, media hacia arriba (data-model.md).
        var average = (total + (count / 2)) / count;
        return new SalesTotals(
            count,
            total,
            average,
            Paid(PaymentMethod.Cash),
            Paid(PaymentMethod.Card),
            Paid(PaymentMethod.Transfer),
            Paid(PaymentMethod.CreditNote),
            Paid(PaymentMethod.OnAccount),
            discounted);
    }

    /// <summary>
    /// Proyecta solo fecha y total y agrupa en memoria por día local, porque SQLite no aplica zonas
    /// horarias; con 10,000 filas de dos columnas cuesta milisegundos (research §2).
    /// </summary>
    private static async Task<IReadOnlyList<DayTotal>> DaysAsync(
        IQueryable<Sale> sales,
        IReadOnlyList<DayWindow> days,
        CancellationToken cancellationToken)
    {
        var rows = await sales.Select(s => new { s.CreatedAt, TotalCents = s.TotalCents - s.ReturnedCents }).ToListAsync(cancellationToken);
        var totals = new long[days.Count];
        var counts = new int[days.Count];
        foreach (var row in rows)
        {
            var index = IndexOfDay(days, row.CreatedAt);
            if (index >= 0)
            {
                totals[index] += row.TotalCents;
                counts[index]++;
            }
        }

        return [.. days.Select((day, i) => new DayTotal(day.LocalDate, totals[i], counts[i]))];
    }

    private static int IndexOfDay(IReadOnlyList<DayWindow> days, DateTime createdAtUtc)
    {
        int low = 0;
        int high = days.Count - 1;
        while (low <= high)
        {
            var middle = (low + high) / 2;
            if (createdAtUtc < days[middle].FromUtc)
            {
                high = middle - 1;
            }
            else if (createdAtUtc >= days[middle].ToUtcExclusive)
            {
                low = middle + 1;
            }
            else
            {
                return middle;
            }
        }

        return -1;
    }

    private async Task<(IReadOnlyList<SalesReportRow> Rows, long Total)> RowsAsync(
        IQueryable<Sale> sales,
        SalesReportQuery query,
        CancellationToken cancellationToken)
    {
        var total = await sales.LongCountAsync(cancellationToken);
        var joined =
            from s in sales
            join u in _context.Users.AsNoTracking() on s.CreatedBy equals u.Id
            select new { s.Id, s.FolioNumber, s.CreatedAt, s.TotalCents, CashierName = u.UserName };

        var ordered = (query.Sort, query.Descending) switch
        {
            (SalesReportSort.Folio, false) => joined.OrderBy(r => r.FolioNumber),
            (SalesReportSort.Folio, true) => joined.OrderByDescending(r => r.FolioNumber),
            (SalesReportSort.Cashier, false) => joined.OrderBy(r => r.CashierName).ThenBy(r => r.FolioNumber),
            (SalesReportSort.Cashier, true) => joined.OrderByDescending(r => r.CashierName).ThenByDescending(r => r.FolioNumber),
            (SalesReportSort.Total, false) => joined.OrderBy(r => r.TotalCents).ThenBy(r => r.FolioNumber),
            (SalesReportSort.Total, true) => joined.OrderByDescending(r => r.TotalCents).ThenByDescending(r => r.FolioNumber),
            (_, false) => joined.OrderBy(r => r.CreatedAt).ThenBy(r => r.FolioNumber),
            _ => joined.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.FolioNumber),
        };

        var skip = (long)(query.Page - 1) * query.PageSize;
        var page = await ordered
            .Skip((int)Math.Min(skip, int.MaxValue))
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return ([.. page.Select(r => new SalesReportRow(r.Id, Folio.Format(r.FolioNumber), r.CreatedAt, r.CashierName, r.TotalCents))], total);
    }

    /// <summary>Línea de venta con su categoría vigente; importes en centavos y unidades en milésimas.</summary>
    private sealed record LineFact(
        Guid SaleId,
        Guid ProductId,
        Guid? CategoryId,
        DateTime CreatedAt,
        long UnitsThousandths,
        long AmountCents,
        long NetCents,
        long DiscountCents);
}
