using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Abstractions;
using Pos.Application.Products;
using Pos.Application.Sales;
using Pos.Domain.Sales;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Sales;

public sealed class SaleRepository : ISaleRepository
{
    // https://www.sqlite.org/rescode.html#constraint_primarykey y #constraint_unique
    private const int SqliteConstraintPrimaryKey = 1555;
    private const int SqliteConstraintUnique = 2067;

    private const int TopProductsCount = 5;

    private readonly PosDbContext _context;

    public SaleRepository(PosDbContext context) => _context = context;

    public Task<bool> ExistsForDraftAsync(Guid draftId, CancellationToken cancellationToken) =>
        _context.Sales.AnyAsync(s => s.DraftId == draftId, cancellationToken);

    public async Task<ConfirmedSale?> FindByDraftAsync(Guid draftId, CancellationToken cancellationToken)
    {
        var sale = await _context.Sales.AsNoTracking()
            .Where(s => s.DraftId == draftId)
            .Select(s => new { s.Id, s.FolioNumber, s.TotalCents })
            .SingleOrDefaultAsync(cancellationToken);
        if (sale is null)
        {
            return null;
        }

        var change = await _context.SalePayments.AsNoTracking()
            .Where(p => p.SaleId == sale.Id && p.Method == PaymentMethod.Cash)
            .Select(p => p.ChangeCents)
            .SingleOrDefaultAsync(cancellationToken);
        return new ConfirmedSale(sale.Id, Folio.Format(sale.FolioNumber), sale.TotalCents, change ?? 0);
    }

    public async Task<long> NextFolioNumberAsync(CancellationToken cancellationToken) =>
        (await _context.Sales.MaxAsync(s => (long?)s.FolioNumber, cancellationToken) ?? 0) + 1;

    public Task<Sale?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Sales
            .Include(s => s.Lines)
            .Include(s => s.Payments)
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

    public void Add(Sale sale) => _context.Sales.Add(sale);

    public async Task<SaveOutcome> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return SaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            _context.ChangeTracker.Clear();
            return SaveOutcome.Conflict;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException
        {
            SqliteExtendedErrorCode: SqliteConstraintUnique or SqliteConstraintPrimaryKey,
        } unique)
        {
            _context.ChangeTracker.Clear();
            return DuplicateOf(unique.Message);
        }
    }

    public async Task<SalePage> SearchAsync(SaleSearch search, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(search);

        var sales = Filtered(search);
        var total = await sales.LongCountAsync(cancellationToken);
        var pageCount = total <= 0 ? 1 : (int)((total + search.PageSize - 1) / search.PageSize);
        var page = Math.Clamp(search.Page, 1, pageCount);

        var rows = await sales
            .OrderByDescending(s => s.CreatedAt)
            .ThenByDescending(s => s.Id)
            .Skip((page - 1) * search.PageSize)
            .Take(search.PageSize)
            .Select(s => new { s.Id, s.FolioNumber, s.CreatedAt, s.TotalCents, s.Status })
            .ToListAsync(cancellationToken);

        var ids = rows.Select(r => r.Id).ToList();
        var methods = (await _context.SalePayments.AsNoTracking()
                .Where(p => ids.Contains(p.SaleId))
                .Select(p => new { p.SaleId, p.Method })
                .ToListAsync(cancellationToken))
            .GroupBy(p => p.SaleId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<PaymentMethod>)[.. g.Select(p => p.Method).Distinct().Order()]);

        var items = rows
            .Select(r => new SaleListItemDto(
                r.Id,
                Folio.Format(r.FolioNumber),
                r.CreatedAt,
                r.TotalCents,
                methods.GetValueOrDefault(r.Id) ?? [],
                r.Status))
            .ToList();
        return new SalePage(items, total, page, search.PageSize);
    }

    public async Task<SaleDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var sale = await _context.Sales.AsNoTracking()
            .Include(s => s.Lines)
            .Include(s => s.Payments)
            .AsSplitQuery()
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (sale is null)
        {
            return null;
        }

        return new SaleDetailDto(
            sale.Id,
            sale.Folio,
            sale.CreatedAt,
            SystemUser.NameOf(sale.CreatedBy),
            sale.TotalCents,
            sale.Status,
            sale.Version,
            sale.CancellationReason,
            sale.CancelledAt,
            sale.CancelledBy is { } by ? SystemUser.NameOf(by) : null,
            [.. sale.Lines.OrderBy(l => l.Position).Select(l => new SaleLineDto(
                l.Position,
                l.ProductId,
                l.ProductName,
                l.ProductSku,
                l.UnitCode,
                l.DecimalPlaces,
                l.UnitPriceCents,
                l.QuantityThousandths,
                l.AmountCents))],
            [.. sale.Payments.OrderBy(p => p.Method).Select(p => new SalePaymentDto(
                p.Method,
                p.AmountCents,
                p.ReceivedCents,
                p.ChangeCents,
                p.Reference))]);
    }

    public async Task<SalesDashboard> GetDashboardAsync(IReadOnlyList<DayWindow> days, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(days);

        var totals = new List<DayTotal>();
        foreach (var day in days)
        {
            var completed = CompletedBetween(day.FromUtc, day.ToUtcExclusive);
            var count = await completed.CountAsync(cancellationToken);
            var cents = await completed.SumAsync(s => (long?)s.TotalCents, cancellationToken) ?? 0;
            totals.Add(new DayTotal(day.LocalDate, cents, count));
        }

        var top = new List<TopProduct>();
        if (days.Count > 0)
        {
            var periodStart = days.Min(d => d.FromUtc);
            var periodEnd = days.Max(d => d.ToUtcExclusive);
            var lines =
                from l in _context.SaleLines.AsNoTracking()
                join s in CompletedBetween(periodStart, periodEnd) on l.SaleId equals s.Id
                select new { l.ProductId, l.ProductName, l.DecimalPlaces, l.QuantityThousandths, s.CreatedAt };

            var ranked = await lines
                .GroupBy(l => l.ProductId)
                .Select(g => new { ProductId = g.Key, Quantity = g.Sum(l => l.QuantityThousandths) })
                .OrderByDescending(x => x.Quantity)
                .ThenBy(x => x.ProductId)
                .Take(TopProductsCount)
                .ToListAsync(cancellationToken);

            foreach (var item in ranked)
            {
                // El nombre que se muestra es el de la línea más reciente del producto.
                var latest = await lines
                    .Where(l => l.ProductId == item.ProductId)
                    .OrderByDescending(l => l.CreatedAt)
                    .Select(l => new { l.ProductName, l.DecimalPlaces })
                    .FirstAsync(cancellationToken);
                top.Add(new TopProduct(item.ProductId, latest.ProductName, item.Quantity, latest.DecimalPlaces));
            }
        }

        return new SalesDashboard(totals, top);
    }

    /// <summary>Predicado común de "Ventas realizadas" y de Inicio (SC-009): completadas en un rango UTC.</summary>
    private IQueryable<Sale> CompletedBetween(DateTime fromUtc, DateTime toUtcExclusive) =>
        _context.Sales.AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed && s.CreatedAt >= fromUtc && s.CreatedAt < toUtcExclusive);

    private IQueryable<Sale> Filtered(SaleSearch search)
    {
        var sales = _context.Sales.AsNoTracking().AsQueryable();
        if (search.FromUtc is { } from)
        {
            sales = sales.Where(s => s.CreatedAt >= from);
        }

        if (search.ToUtcExclusive is { } to)
        {
            sales = sales.Where(s => s.CreatedAt < to);
        }

        if (search.FolioNumber is { } folio)
        {
            sales = sales.Where(s => s.FolioNumber == folio);
        }

        if (search.Status is { } status)
        {
            sales = sales.Where(s => s.Status == status);
        }

        return sales;
    }

    /// <summary>Distingue qué índice único se violó a partir del mensaje de SQLite.</summary>
    private static SaveOutcome DuplicateOf(string message) => message switch
    {
        _ when message.Contains("Sales.DraftId", StringComparison.Ordinal) => SaveOutcome.Duplicate(SaleFields.DraftId),
        _ when message.Contains("Sales.FolioNumber", StringComparison.Ordinal) => SaveOutcome.Duplicate(SaleFields.Folio),
        _ => SaveOutcome.Conflict,
    };
}
