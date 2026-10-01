using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Abstractions;
using Pos.Application.CashShifts;
using Pos.Application.Products;
using Pos.Application.Returns;
using Pos.Application.Sales;
using Pos.Domain.CashShifts;
using Pos.Domain.Returns;
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
            .Select(s => new
            {
                s.Id,
                s.FolioNumber,
                s.CreatedAt,
                s.TotalCents,
                s.Status,
                s.CreatedBy,
                CashierName = _context.Users.Where(u => u.Id == s.CreatedBy).Select(u => u.FullName).FirstOrDefault(),
            })
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
                r.Status,
                r.CashierName ?? SystemUser.NameOf(r.CreatedBy)))
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

        // Nombres con LEFT JOIN a Users; si no aparece, el id abreviado (007, research §3).
        var returns = await _context.SaleReturns.AsNoTracking()
            .Where(r => r.SaleId == id)
            .OrderBy(r => r.Number)
            .Select(r => new
            {
                r.Id,
                r.Number,
                r.CreatedAt,
                r.CreatedBy,
                r.Reason,
                r.AuthorizedBy,
                r.TotalCents,
                r.Kind,
                r.Compensation,
                NoteNumber = _context.CreditNotes.Where(n => n.Id == r.CreditNoteId).Select(n => (long?)n.Number).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var userIds = new[] { sale.CreatedBy, sale.CancelledBy ?? sale.CreatedBy }
            .Concat(returns.SelectMany(r => new[] { r.CreatedBy, r.AuthorizedBy }))
            .Distinct()
            .ToList();
        var names = await _context.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
        string NameOf(Guid userId) => names.GetValueOrDefault(userId) ?? SystemUser.NameOf(userId);

        return new SaleDetailDto(
            sale.Id,
            sale.Folio,
            sale.CreatedAt,
            NameOf(sale.CreatedBy),
            sale.TotalCents,
            sale.Status,
            sale.Version,
            sale.CancellationReason,
            sale.CancelledAt,
            sale.CancelledBy is { } by ? NameOf(by) : null,
            [.. sale.Lines.OrderBy(l => l.Position).Select(l => new SaleLineDto(
                l.Position,
                l.ProductId,
                l.ProductName,
                l.ProductSku,
                l.UnitCode,
                l.DecimalPlaces,
                l.UnitPriceCents,
                l.QuantityThousandths,
                l.AmountCents,
                l.Id,
                l.ReturnedQuantity))],
            [.. sale.Payments.OrderBy(p => p.Method).Select(p => new SalePaymentDto(
                p.Method,
                p.AmountCents,
                p.ReceivedCents,
                p.ChangeCents,
                p.Reference))],
            sale.CreatedBy,
            sale.ReturnedCents,
            [.. returns.Select(r => new ReturnSummaryDto(
                r.Id,
                ReturnFolio.Format(r.Number),
                r.CreatedAt,
                NameOf(r.CreatedBy),
                r.Reason,
                NameOf(r.AuthorizedBy),
                r.TotalCents,
                r.Kind,
                r.Compensation,
                r.NoteNumber is { } note ? Pos.Domain.CreditNotes.CreditNoteFolio.Format(note) : null))]);
    }

    public async Task<ShiftSalesTotals> GetShiftTotalsAsync(Guid shiftId, CancellationToken cancellationToken)
    {
        var sales = _context.Sales.AsNoTracking().Where(s => s.CashShiftId == shiftId);

        // Desde 013 el total vendido es neto de devoluciones parciales (research §11).
        var byStatus = await sales
            .GroupBy(s => s.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Cents = g.Sum(s => s.TotalCents - s.ReturnedCents) })
            .ToListAsync(cancellationToken);
        var payments = await (
                from p in _context.SalePayments.AsNoTracking()
                join s in sales on p.SaleId equals s.Id
                select new { s.Id, s.Status, p.Method, p.AmountCents })
            .ToListAsync(cancellationToken);

        // Canceladas con devolución registrada: su efectivo ya salió (o se conservó) por el reintegro;
        // solo las canceladas heredadas (sin devolución) restan su efectivo (research §6).
        var cancelledWithReturn = (await (
                from r in _context.SaleReturns.AsNoTracking()
                join s in sales on r.SaleId equals s.Id
                where s.Status == SaleStatus.Cancelled
                select s.Id)
            .ToListAsync(cancellationToken)).ToHashSet();

        long Paid(PaymentMethod method, SaleStatus? status = null) =>
            payments.Where(p => p.Method == method && (status is null || p.Status == status)).Sum(p => p.AmountCents);

        var refunds = await (
                from f in _context.SaleReturnRefunds.AsNoTracking()
                join r in _context.SaleReturns.AsNoTracking() on f.SaleReturnId equals r.Id
                where r.CashShiftId == shiftId
                select new { f.Method, f.AmountCents })
            .ToListAsync(cancellationToken);
        var notesIssued = await _context.SaleReturns.AsNoTracking()
            .Where(r => r.CashShiftId == shiftId && r.Compensation == ReturnCompensation.CreditNote)
            .SumAsync(r => (long?)r.TotalCents, cancellationToken) ?? 0;

        var completed = byStatus.FirstOrDefault(x => x.Status == SaleStatus.Completed);
        var cancelled = byStatus.FirstOrDefault(x => x.Status == SaleStatus.Cancelled);
        return new ShiftSalesTotals(
            completed?.Count ?? 0,
            cancelled?.Count ?? 0,
            completed?.Cents ?? 0,
            Paid(PaymentMethod.Cash),
            payments
                .Where(p => p.Method == PaymentMethod.Cash && p.Status == SaleStatus.Cancelled && !cancelledWithReturn.Contains(p.Id))
                .Sum(p => p.AmountCents),
            Paid(PaymentMethod.Card, SaleStatus.Completed),
            Paid(PaymentMethod.Transfer, SaleStatus.Completed),
            refunds.Where(f => f.Method == PaymentMethod.Cash).Sum(f => f.AmountCents),
            refunds.Where(f => f.Method is PaymentMethod.Card or PaymentMethod.Transfer).Sum(f => f.AmountCents),
            notesIssued);
    }

    public async Task<IReadOnlyList<ShiftSaleRowDto>> ListByShiftAsync(Guid shiftId, CancellationToken cancellationToken)
    {
        var rows = await _context.Sales.AsNoTracking()
            .Where(s => s.CashShiftId == shiftId)
            .OrderBy(s => s.CreatedAt)
            .ThenBy(s => s.Id)
            .Select(s => new { s.Id, s.FolioNumber, s.CreatedAt, s.TotalCents, s.Status })
            .ToListAsync(cancellationToken);

        var ids = rows.Select(r => r.Id).ToList();
        var methods = (await _context.SalePayments.AsNoTracking()
                .Where(p => ids.Contains(p.SaleId))
                .Select(p => new { p.SaleId, p.Method })
                .ToListAsync(cancellationToken))
            .GroupBy(p => p.SaleId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<PaymentMethod>)[.. g.Select(p => p.Method).Distinct().Order()]);

        return [.. rows.Select(r => new ShiftSaleRowDto(
            r.Id,
            Folio.Format(r.FolioNumber),
            r.CreatedAt,
            r.TotalCents,
            methods.GetValueOrDefault(r.Id) ?? [],
            r.Status))];
    }

    public async Task<long> GetCashAppliedAsync(Guid saleId, CancellationToken cancellationToken) =>
        await _context.SalePayments.AsNoTracking()
            .Where(p => p.SaleId == saleId && p.Method == PaymentMethod.Cash)
            .SumAsync(p => (long?)p.AmountCents, cancellationToken) ?? 0;

    public async Task<SalesDashboard> GetDashboardAsync(IReadOnlyList<DayWindow> days, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(days);

        var totals = new List<DayTotal>();
        foreach (var day in days)
        {
            var completed = CompletedBetween(day.FromUtc, day.ToUtcExclusive);
            var count = await completed.CountAsync(cancellationToken);
            var cents = await completed.SumAsync(s => (long?)(s.TotalCents - s.ReturnedCents), cancellationToken) ?? 0;
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
                select new { l.ProductId, l.ProductName, l.DecimalPlaces, QuantityThousandths = l.QuantityThousandths - l.ReturnedQuantity, s.CreatedAt };

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

        if (search.CashierId is { } cashier)
        {
            sales = sales.Where(s => s.CreatedBy == cashier);
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
