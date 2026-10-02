using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Products;
using Pos.Application.Suppliers;
using Pos.Domain.Suppliers;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Suppliers;

public sealed class SupplierRepository : ISupplierRepository
{
    // https://www.sqlite.org/rescode.html#constraint_unique
    private const int SqliteConstraintUnique = 2067;
    private const string LikeEscape = @"\";

    private readonly PosDbContext _context;

    public SupplierRepository(PosDbContext context) => _context = context;

    public Task<Supplier?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Suppliers.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<Supplier?> FindByTaxIdAsync(string taxId, CancellationToken cancellationToken) =>
        _context.Suppliers.AsNoTracking().SingleOrDefaultAsync(s => s.TaxId == taxId, cancellationToken);

    public void Add(Supplier supplier) => _context.Suppliers.Add(supplier);

    public async Task<SupplierPage> SearchAsync(SupplierSearch search, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(search);

        // IsActive primero, como en el índice IX_Suppliers_Active_Search.
        var suppliers = _context.Suppliers.AsNoTracking();
        if (!search.IncludeInactive)
        {
            suppliers = suppliers.Where(s => s.IsActive);
        }

        if (!string.IsNullOrEmpty(search.Text))
        {
            var pattern = LikeContains(search.Text);
            suppliers = suppliers.Where(s => EF.Functions.Like(s.SearchText, pattern, LikeEscape));
        }

        var total = await suppliers.LongCountAsync(cancellationToken);
        var pageCount = total <= 0 ? 1 : (int)((total + search.PageSize - 1) / search.PageSize);
        var page = Math.Clamp(search.Page, 1, pageCount);

        var items = await suppliers
            .OrderBy(s => s.SearchText)
            .ThenBy(s => s.Id)
            .Skip((page - 1) * search.PageSize)
            .Take(search.PageSize)
            .Select(s => new SupplierListItemDto(s.Id, s.Name, s.TaxId, s.Phone, s.PaymentTerms, s.CreditDays, s.IsActive, s.Version))
            .ToListAsync(cancellationToken);
        return new SupplierPage(items, total, page, search.PageSize);
    }

    public async Task<IReadOnlyList<SupplierFilterOption>> ListForFilterAsync(CancellationToken cancellationToken) =>
        await _context.Suppliers.AsNoTracking()
            .OrderBy(s => s.SearchText)
            .ThenBy(s => s.Id)
            .Select(s => new SupplierFilterOption(s.Id, s.Name, s.IsActive))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SupplierOption>> ListActiveAsync(string? searchText, int limit, CancellationToken cancellationToken)
    {
        var suppliers = _context.Suppliers.AsNoTracking().Where(s => s.IsActive);
        if (!string.IsNullOrEmpty(searchText))
        {
            var pattern = LikeContains(searchText);
            suppliers = suppliers.Where(s => EF.Functions.Like(s.SearchText, pattern, LikeEscape));
        }

        return await suppliers
            .OrderBy(s => s.SearchText)
            .ThenBy(s => s.Id)
            .Take(limit)
            .Select(s => new SupplierOption(s.Id, s.Name, s.TaxId))
            .ToListAsync(cancellationToken);
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
            return unique.Message.Contains("Suppliers.TaxId", StringComparison.Ordinal)
                ? SaveOutcome.Duplicate(SupplierFields.TaxId)
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
