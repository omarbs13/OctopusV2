using Microsoft.EntityFrameworkCore;
using Pos.Application.Reports;
using Pos.Application.Reports.GetSalesReport;
using Pos.Application.Sales;
using Pos.Domain.Sales;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Reports;

/// <summary>
/// Reporte de ventas con consultas agregadas sin seguimiento de cambios (research §1, §2). Solo cuenta
/// ventas completadas; los pagos son netos de cambio, así que efectivo + tarjeta + transferencia = total.
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
        var totals = await TotalsAsync(sales, cancellationToken);
        var comparison = window.Previous is { } previous
            ? new SalesComparison(await TotalsAsync(Completed(previous, query.CashierId), cancellationToken), null)
            : null;

        var days = await DaysAsync(sales, window.Days, cancellationToken);
        var (rows, totalRows) = await RowsAsync(sales, query, cancellationToken);
        return new SalesReport(totals, comparison, days, rows, totalRows, query.Page, query.PageSize);
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
            select new { s.Id, s.FolioNumber, s.CreatedAt, s.TotalCents, CashierName = u.FullName };

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
}
