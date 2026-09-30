using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Products;
using Pos.Domain.Products;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Products;

public sealed class ProductRepository : IProductRepository
{
    // SQLITE_CONSTRAINT_UNIQUE: https://www.sqlite.org/rescode.html#constraint_unique
    private const int SqliteConstraintUnique = 2067;

    /// <summary>Carácter de escape para LIKE.</summary>
    private const string LikeEscape = @"\";

    private readonly PosDbContext _context;

    public ProductRepository(PosDbContext context) => _context = context;

    public Task<Product?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Products.SingleOrDefaultAsync(p => p.Id == id && p.DeletedAt == null, cancellationToken);

    public Task<bool> SkuExistsAsync(string sku, Guid? excludingId, CancellationToken cancellationToken) =>
        _context.Products.AnyAsync(
            p => p.DeletedAt == null && p.Sku == sku && (excludingId == null || p.Id != excludingId),
            cancellationToken);

    public Task<bool> BarcodeExistsAsync(string barcode, Guid? excludingId, CancellationToken cancellationToken) =>
        _context.Products.AnyAsync(
            p => p.DeletedAt == null && p.Barcode == barcode && (excludingId == null || p.Id != excludingId),
            cancellationToken);

    public async Task<ProductSearchPage> SearchAsync(ProductSearch search, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(search);

        var query = _context.Products.AsNoTracking().Where(p => p.DeletedAt == null);
        if (!search.IncludeInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        if (search.NameText is not null)
        {
            var namePattern = LikeContains(search.NameText);
            var skuPattern = LikeContains(search.SkuText ?? search.NameText);
            var barcodeText = search.BarcodeText ?? search.NameText;
            var barcodePattern = LikeContains(barcodeText);

            query = search.BarcodeExact
                ? query.Where(p =>
                    EF.Functions.Like(p.NameSearch, namePattern, LikeEscape)
                    || EF.Functions.Like(p.Sku, skuPattern, LikeEscape)
                    || p.Barcode == barcodeText)
                : query.Where(p =>
                    EF.Functions.Like(p.NameSearch, namePattern, LikeEscape)
                    || EF.Functions.Like(p.Sku, skuPattern, LikeEscape)
                    || EF.Functions.Like(p.Barcode, barcodePattern, LikeEscape));
        }

        var rows = await query
            .OrderBy(p => p.NameSearch)
            .ThenBy(p => p.Sku)
            .Take(search.Limit + 1)
            .Select(p => new ProductListItemDto(p.Id, p.Name, p.Sku, p.Barcode, p.Price.Cents, p.IsActive, p.Version))
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > search.Limit;
        return new ProductSearchPage(hasMore ? rows.Take(search.Limit).ToList() : rows, hasMore);
    }

    public void Add(Product product) => _context.Products.Add(product);

    public async Task<SaveOutcome> SaveChangesAsync(Product product, int? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(product);

        if (expectedVersion is { } version)
        {
            // La comprobación de concurrencia compara contra la versión que vio el operador.
            _context.Entry(product).Property(p => p.Version).OriginalValue = version;
        }

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
            return SaveOutcome.Duplicate(DuplicateField(unique.Message));
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

    /// <summary>SQLite indica en el mensaje la columna del índice violado: "UNIQUE constraint failed: Products.Sku".</summary>
    private static string DuplicateField(string message) =>
        message.Contains("Products.Barcode", StringComparison.Ordinal) ? ProductFields.Barcode : ProductFields.Sku;
}
