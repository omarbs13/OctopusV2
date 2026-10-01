using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Customers;
using Pos.Application.Products;
using Pos.Domain.Customers;
using Pos.Domain.Receivables;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Customers;

public sealed class CustomerRepository : ICustomerRepository
{
    // https://www.sqlite.org/rescode.html#constraint_unique
    private const int SqliteConstraintUnique = 2067;
    private const string LikeEscape = @"\";

    private readonly PosDbContext _context;

    public CustomerRepository(PosDbContext context) => _context = context;

    public Task<Customer?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Customers.SingleOrDefaultAsync(c => c.Id == id && c.DeletedAt == null, cancellationToken);

    public void Add(Customer customer) => _context.Customers.Add(customer);

    public async Task<CustomerPage> SearchAsync(CustomerSearch search, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(search);

        var customers = _context.Customers.AsNoTracking().Where(c => c.DeletedAt == null);
        if (!search.IncludeInactive)
        {
            customers = customers.Where(c => c.IsActive);
        }

        if (!string.IsNullOrEmpty(search.Text))
        {
            var pattern = LikeContains(search.Text);
            customers = customers.Where(c => EF.Functions.Like(c.SearchText, pattern, LikeEscape));
        }

        var total = await customers.LongCountAsync(cancellationToken);
        var pageCount = total <= 0 ? 1 : (int)((total + search.PageSize - 1) / search.PageSize);
        var page = Math.Clamp(search.Page, 1, pageCount);

        var items = await customers
            .OrderBy(c => c.SearchText)
            .ThenBy(c => c.Id)
            .Skip((page - 1) * search.PageSize)
            .Take(search.PageSize)
            .Select(c => new CustomerListItemDto(c.Id, c.Name, c.Phone, c.TaxId, c.CreditMode, c.CreditLimitCents, 0, c.IsActive))
            .ToListAsync(cancellationToken);
        return new CustomerPage(items, total, page, search.PageSize);
    }

    public async Task<IReadOnlyList<Customer>> FindForSaleAsync(string searchText, int limit, CancellationToken cancellationToken)
    {
        var customers = _context.Customers.AsNoTracking()
            .Where(c => c.DeletedAt == null && c.IsActive && c.CreditMode == CreditMode.Credit);
        if (!string.IsNullOrEmpty(searchText))
        {
            var pattern = LikeContains(searchText);
            customers = customers.Where(c => EF.Functions.Like(c.SearchText, pattern, LikeEscape));
        }

        return await customers.OrderBy(c => c.SearchText).ThenBy(c => c.Id).Take(limit).ToListAsync(cancellationToken);
    }

    public async Task<long> GetBalanceAsync(Guid customerId, CancellationToken cancellationToken) =>
        await _context.Receivables.AsNoTracking()
            .Where(r => r.CustomerId == customerId && r.Status == ReceivableStatus.Pending)
            .SumAsync(r => (long?)r.BalanceCents, cancellationToken) ?? 0;

    public async Task<IReadOnlyDictionary<Guid, long>> GetBalancesAsync(IReadOnlyCollection<Guid> customerIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(customerIds);
        if (customerIds.Count == 0)
        {
            return new Dictionary<Guid, long>();
        }

        var rows = await _context.Receivables.AsNoTracking()
            .Where(r => customerIds.Contains(r.CustomerId) && r.Status == ReceivableStatus.Pending)
            .GroupBy(r => r.CustomerId)
            .Select(g => new { CustomerId = g.Key, Cents = g.Sum(r => r.BalanceCents) })
            .ToListAsync(cancellationToken);
        var balances = rows.ToDictionary(r => r.CustomerId, r => r.Cents);
        foreach (var id in customerIds)
        {
            balances.TryAdd(id, 0);
        }

        return balances;
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
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteExtendedErrorCode: SqliteConstraintUnique } unique)
        {
            _context.ChangeTracker.Clear();
            return unique.Message.Contains("Customers.TaxId", StringComparison.Ordinal)
                ? SaveOutcome.Duplicate(CustomerFields.TaxId)
                : SaveOutcome.Conflict;
        }
    }

    /// <summary>Patrón LIKE de "contiene", escapando los comodines del texto buscado.</summary>
    private static string LikeContains(string text)
    {
        var escaped = text
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
        return $"%{escaped}%";
    }
}
