using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Abstractions;
using Pos.Application.Products;
using Pos.Application.Returns;
using Pos.Domain.Returns;
using Pos.Domain.Sales;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Returns;

public sealed class ReturnRepository : IReturnRepository
{
    // https://www.sqlite.org/rescode.html#constraint_primarykey y #constraint_unique
    private const int SqliteConstraintPrimaryKey = 1555;
    private const int SqliteConstraintUnique = 2067;

    private readonly PosDbContext _context;

    public ReturnRepository(PosDbContext context) => _context = context;

    public async Task<long> NextNumberAsync(CancellationToken cancellationToken) =>
        (await _context.SaleReturns.MaxAsync(r => (long?)r.Number, cancellationToken) ?? 0) + 1;

    public void Add(SaleReturn saleReturn) => _context.SaleReturns.Add(saleReturn);

    public Task<SaleReturnRefund?> GetRefundAsync(Guid refundId, CancellationToken cancellationToken) =>
        _context.SaleReturnRefunds.SingleOrDefaultAsync(f => f.Id == refundId, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, long>> GetReturnedByPaymentAsync(Guid saleId, CancellationToken cancellationToken)
    {
        var rows = await (
                from f in _context.SaleReturnRefunds.AsNoTracking()
                join r in _context.SaleReturns.AsNoTracking() on f.SaleReturnId equals r.Id
                where r.SaleId == saleId
                group f by f.SalePaymentId into g
                select new { PaymentId = g.Key, Cents = g.Sum(x => x.AmountCents) })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(x => x.PaymentId, x => x.Cents);
    }

    public async Task<ReversalPage> SearchReversalsAsync(ReversalSearch search, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(search);

        // Solo tarjeta y transferencia llegan a pendiente o reversado.
        var refunds = _context.SaleReturnRefunds.AsNoTracking()
            .Where(f => f.Status == RefundStatus.PendingReversal || f.Status == RefundStatus.Reversed);
        refunds = search.Filter switch
        {
            ReversalFilter.Pending => refunds.Where(f => f.Status == RefundStatus.PendingReversal),
            ReversalFilter.Reversed => refunds.Where(f => f.Status == RefundStatus.Reversed),
            _ => refunds,
        };

        var joined =
            from f in refunds
            join r in _context.SaleReturns.AsNoTracking() on f.SaleReturnId equals r.Id
            join s in _context.Sales.AsNoTracking() on r.SaleId equals s.Id
            select new { f, r.Number, r.CreatedAt, s.FolioNumber };

        var total = await joined.LongCountAsync(cancellationToken);
        var pageCount = total <= 0 ? 1 : (int)((total + search.PageSize - 1) / search.PageSize);
        var page = Math.Clamp(search.Page, 1, pageCount);

        var rows = await joined
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.f.Id)
            .Skip((page - 1) * search.PageSize)
            .Take(search.PageSize)
            .Select(x => new
            {
                x.f.Id,
                x.Number,
                x.FolioNumber,
                x.f.Method,
                x.f.AmountCents,
                x.CreatedAt,
                x.f.Status,
                x.f.ReversedBy,
                ReversedByName = _context.Users.Where(u => u.Id == x.f.ReversedBy).Select(u => u.UserName).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(x => new PendingReversalDto(
            x.Id,
            ReturnFolio.Format(x.Number),
            Folio.Format(x.FolioNumber),
            x.Method,
            x.AmountCents,
            x.CreatedAt,
            x.Status,
            x.ReversedBy is { } by ? x.ReversedByName ?? SystemUser.NameOf(by) : null))
            .ToList();
        return new ReversalPage(items, total, page, search.PageSize);
    }

    public async Task<(string ReturnFolio, string SaleFolio, Guid SaleId)?> DescribeRefundAsync(Guid refundId, CancellationToken cancellationToken)
    {
        var row = await (
                from f in _context.SaleReturnRefunds.AsNoTracking()
                join r in _context.SaleReturns.AsNoTracking() on f.SaleReturnId equals r.Id
                join s in _context.Sales.AsNoTracking() on r.SaleId equals s.Id
                where f.Id == refundId
                select new { r.Number, s.FolioNumber, SaleId = s.Id })
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : (ReturnFolio.Format(row.Number), Folio.Format(row.FolioNumber), row.SaleId);
    }

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
        })
        {
            _context.ChangeTracker.Clear();
            return SaveOutcome.Conflict;
        }
    }
}
