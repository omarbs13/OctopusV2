using Microsoft.EntityFrameworkCore;
using Pos.Application.Receivables;
using Pos.Domain.Receivables;
using Pos.Domain.Sales;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Receivables;

/// <summary>Cuentas por cobrar con su libro; se guardan con la unidad de trabajo del caso de uso.</summary>
public sealed class ReceivableRepository : IReceivableRepository
{
    private readonly PosDbContext _context;

    public ReceivableRepository(PosDbContext context) => _context = context;

    public Task<Receivable?> GetBySaleAsync(Guid saleId, CancellationToken cancellationToken) =>
        _context.Receivables.Include(r => r.Entries).SingleOrDefaultAsync(r => r.SaleId == saleId, cancellationToken);

    public async Task<IReadOnlyList<Receivable>> GetPendingAsync(Guid customerId, CancellationToken cancellationToken) =>
        await _context.Receivables
            .Include(r => r.Entries)
            .Where(r => r.CustomerId == customerId && r.Status == ReceivableStatus.Pending)
            .OrderBy(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Receivable>> GetManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await _context.Receivables
            .Include(r => r.Entries)
            .Where(r => ids.Contains(r.Id))
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

    public void Add(Receivable receivable) => _context.Receivables.Add(receivable);

    public async Task<IReadOnlyList<ReceivableEntry>> GetEntriesByPaymentAsync(Guid paymentId, CancellationToken cancellationToken) =>
        await _context.ReceivableEntries.AsNoTracking()
            .Where(e => e.CustomerPaymentId == paymentId)
            .ToListAsync(cancellationToken);

    /// <remarks>
    /// Compara con <c>&gt;=</c>: ante una devolución en el mismo instante que el abono se rechaza la
    /// anulación, que es lo seguro (research §7).
    /// </remarks>
    public Task<bool> HasReturnAfterAsync(IReadOnlyCollection<Guid> receivableIds, DateTime afterUtc, CancellationToken cancellationToken) =>
        _context.ReceivableEntries.AsNoTracking()
            .AnyAsync(
                e => receivableIds.Contains(e.ReceivableId)
                    && (e.Type == ReceivableEntryType.Return || e.Type == ReceivableEntryType.ExcessOut)
                    && e.CreatedAt >= afterUtc,
                cancellationToken);

    public async Task<DateTime?> GetOldestPendingDateAsync(Guid customerId, CancellationToken cancellationToken) =>
        await _context.Receivables.AsNoTracking()
            .Where(r => r.CustomerId == customerId && r.Status == ReceivableStatus.Pending)
            .OrderBy(r => r.CreatedAt)
            .Select(r => (DateTime?)r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<ReceivablePage> ListByCustomerAsync(
        Guid customerId,
        bool onlyPending,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var receivables = _context.Receivables.AsNoTracking().Where(r => r.CustomerId == customerId);
        if (onlyPending)
        {
            receivables = receivables.Where(r => r.Status == ReceivableStatus.Pending);
        }

        var total = await receivables.LongCountAsync(cancellationToken);
        var pageCount = total <= 0 ? 1 : (int)((total + pageSize - 1) / pageSize);
        var current = Math.Clamp(page, 1, pageCount);

        var rows = await (
                from r in receivables
                join s in _context.Sales.AsNoTracking() on r.SaleId equals s.Id
                orderby r.CreatedAt descending, r.Id descending
                select new { r.Id, r.SaleId, s.FolioNumber, s.CreatedAt, r.OriginalCents, r.BalanceCents, r.Status })
            .Skip((current - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new ReceivableRowDto(r.Id, r.SaleId, Folio.Format(r.FolioNumber), r.CreatedAt, r.OriginalCents, r.BalanceCents, r.Status, 0))
            .ToList();
        return new ReceivablePage(items, total, current, pageSize);
    }
}
