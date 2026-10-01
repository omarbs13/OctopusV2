using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Products;
using Pos.Application.Receivables;
using Pos.Domain.Receivables;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Receivables;

public sealed class CustomerPaymentRepository : ICustomerPaymentRepository
{
    // https://www.sqlite.org/rescode.html#constraint_primarykey y #constraint_unique
    private const int SqliteConstraintPrimaryKey = 1555;
    private const int SqliteConstraintUnique = 2067;

    private readonly PosDbContext _context;

    public CustomerPaymentRepository(PosDbContext context) => _context = context;

    public async Task<long> NextNumberAsync(CancellationToken cancellationToken) =>
        (await _context.CustomerPayments.MaxAsync(p => (long?)p.Number, cancellationToken) ?? 0) + 1;

    public Task<CustomerPayment?> FindByRequestAsync(Guid requestId, CancellationToken cancellationToken) =>
        _context.CustomerPayments.AsNoTracking().SingleOrDefaultAsync(p => p.RequestId == requestId, cancellationToken);

    public Task<CustomerPayment?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        _context.CustomerPayments.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    public void Add(CustomerPayment payment) => _context.CustomerPayments.Add(payment);

    public async Task<CustomerPaymentPage> ListByCustomerAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var payments = _context.CustomerPayments.AsNoTracking().Where(p => p.CustomerId == customerId);
        var total = await payments.LongCountAsync(cancellationToken);
        var pageCount = total <= 0 ? 1 : (int)((total + pageSize - 1) / pageSize);
        var current = Math.Clamp(page, 1, pageCount);

        var rows = await payments
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Number)
            .Skip((current - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rows.Select(p => new CustomerPaymentRowDto(
            p.Id,
            p.Folio,
            p.CreatedAt,
            p.AmountCents,
            p.Method,
            p.Reference,
            p.BalanceBeforeCents,
            p.BalanceAfterCents,
            p.Status,
            p.VoidReason))
            .ToList();
        return new CustomerPaymentPage(items, total, current, pageSize);
    }

    public async Task<CustomerPaymentReceiptData?> GetReceiptDataAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var row = await (
                from p in _context.CustomerPayments.AsNoTracking()
                join c in _context.Customers.AsNoTracking() on p.CustomerId equals c.Id
                where p.Id == paymentId
                select new { Payment = p, c.Name })
            .SingleOrDefaultAsync(cancellationToken);
        return row is null
            ? null
            : new CustomerPaymentReceiptData(
                row.Payment.Folio,
                row.Payment.CreatedAt,
                row.Name,
                row.Payment.AmountCents,
                row.Payment.Method,
                row.Payment.Reference,
                row.Payment.BalanceBeforeCents,
                row.Payment.BalanceAfterCents,
                row.Payment.Status == CustomerPaymentStatus.Voided);
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
        } unique)
        {
            _context.ChangeTracker.Clear();
            return unique.Message switch
            {
                var m when m.Contains("CustomerPayments.Number", StringComparison.Ordinal) => SaveOutcome.Duplicate(ReceivableFields.Number),
                var m when m.Contains("CustomerPayments.RequestId", StringComparison.Ordinal) => SaveOutcome.Duplicate(ReceivableFields.RequestId),
                _ => SaveOutcome.Conflict,
            };
        }
    }
}
