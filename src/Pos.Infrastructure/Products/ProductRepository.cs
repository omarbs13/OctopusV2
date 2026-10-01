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

    private readonly PosDbContext _context;

    public ProductRepository(PosDbContext context) => _context = context;

    public Task<Product?> GetAsync(Guid id, bool includeImage, CancellationToken cancellationToken)
    {
        IQueryable<Product> products = includeImage ? _context.Products.Include(p => p.Image) : _context.Products;
        return products.SingleOrDefaultAsync(p => p.Id == id && p.DeletedAt == null, cancellationToken);
    }

    public Task<long> CountActiveAsync(CancellationToken cancellationToken) =>
        _context.Products.LongCountAsync(p => p.IsActive && p.DeletedAt == null, cancellationToken);

    public Task<bool> SkuExistsAsync(string sku, Guid? excludingId, CancellationToken cancellationToken) =>
        _context.Products.AnyAsync(
            p => p.DeletedAt == null && p.Sku == sku && (excludingId == null || p.Id != excludingId),
            cancellationToken);

    public Task<bool> BarcodeExistsAsync(string barcode, Guid? excludingId, CancellationToken cancellationToken) =>
        _context.Products.AnyAsync(
            p => p.DeletedAt == null && p.Barcode == barcode && (excludingId == null || p.Id != excludingId),
            cancellationToken);

    public Task<bool> ExistsWithCodeAsync(string code, CancellationToken cancellationToken)
    {
        // El SKU se guarda en mayúsculas; el código de barras solo tiene dígitos.
        var sku = Product.NormalizeSku(code);
        var barcode = Product.NormalizeBarcode(code);
        return _context.Products.AnyAsync(
            p => p.DeletedAt == null && (p.Sku == sku || (barcode != null && p.Barcode == barcode)),
            cancellationToken);
    }

    public async Task<ProductPage> SearchAsync(ProductSearch search, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(search);

        var query = Visible(search);
        var total = await query.LongCountAsync(cancellationToken);

        // Una página fuera de rango (por ejemplo, tras borrar el último registro) se ajusta a la última válida.
        var page = Math.Clamp(search.Page, 1, ProductPage.PageCount(total, search.PageSize));

        var items = await (
                from p in query
                join u in _context.UnitsOfMeasure on p.UnitCode equals u.Code
                join s in _context.ProductStocks on p.Id equals s.ProductId into stocks
                from s in stocks.DefaultIfEmpty()
                orderby p.NameSearch, p.Sku
                select new ProductListItemDto(
                    p.Id,
                    p.Name,
                    p.Sku,
                    p.Barcode,
                    p.Price.Cents,
                    p.UnitCode,
                    u.Name,
                    p.IsActive,
                    p.Version,
                    p.Image != null ? p.Image.Thumbnail : null,
                    p.TracksInventory,
                    p.TracksInventory ? (s == null ? 0L : s.OnHandThousandths) : null,
                    u.DecimalPlaces))
            .Skip((page - 1) * search.PageSize)
            .Take(search.PageSize)
            .ToListAsync(cancellationToken);

        return new ProductPage(items, total, page, search.PageSize);
    }

    public async Task<int?> LocatePageAsync(ProductSearch search, Guid productId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(search);

        var query = Visible(search);
        var key = await query
            .Where(p => p.Id == productId)
            .Select(p => new { p.NameSearch, p.Sku })
            .SingleOrDefaultAsync(cancellationToken);
        if (key is null)
        {
            return null;
        }

        // Mismo orden que el listado: comparación binaria de SQLite sobre (NameSearch, Sku).
        // EF Core traduce string.Compare a "<" en SQL; la sobrecarga con StringComparison no se traduce.
#pragma warning disable CA1309
        var before = await query.LongCountAsync(
            p => string.Compare(p.NameSearch, key.NameSearch) < 0
                || (p.NameSearch == key.NameSearch && string.Compare(p.Sku, key.Sku) < 0),
            cancellationToken);
#pragma warning restore CA1309
        return (int)(before / search.PageSize) + 1;
    }

    public async Task<IReadOnlyList<Product>> FindForSaleAsync(string code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);
        var barcode = Product.NormalizeBarcode(code);
        var sku = Product.NormalizeSku(code);
        return await _context.Products.AsNoTracking()
            .Where(p => p.Sku == sku || (barcode != null && p.Barcode == barcode))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> SearchForSaleAsync(
        string nameText,
        int limit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nameText);
        var products = _context.Products.AsNoTracking().Where(p => p.DeletedAt == null);
        products = ProductTextFilter.Apply(products, nameText, nameText.ToUpperInvariant(), nameText, barcodeExact: false);
        return await products
            .OrderByDescending(p => p.IsActive)
            .ThenBy(p => p.NameSearch)
            .ThenBy(p => p.Sku)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetManyAsync(
        IReadOnlyCollection<Guid> ids,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var products = _context.Products.AsNoTracking().Where(p => ids.Contains(p.Id));
        if (!includeDeleted)
        {
            products = products.Where(p => p.DeletedAt == null);
        }

        return await products.ToListAsync(cancellationToken);
    }

    public void Add(Product product) => _context.Products.Add(product);

    public async Task<SaveOutcome> SaveChangesAsync(Product product, int? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(product);

        if (product.ImageChanged)
        {
            // Cambiar solo la imagen también es modificar el producto: versión y fecha (FR-027).
            var entry = _context.Entry(product);
            if (entry.State == EntityState.Unchanged)
            {
                entry.State = EntityState.Modified;
            }
        }

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

    /// <summary>Productos no borrados que cumplen el filtro de estado y el texto buscado.</summary>
    private IQueryable<Product> Visible(ProductSearch search)
    {
        var query = _context.Products.AsNoTracking().Where(p => p.DeletedAt == null);
        if (!search.IncludeInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        query = ProductTextFilter.Apply(query, search.NameText, search.SkuText, search.BarcodeText, search.BarcodeExact);

        return query;
    }

    /// <summary>SQLite indica en el mensaje la columna del índice violado: "UNIQUE constraint failed: Products.Sku".</summary>
    private static string DuplicateField(string message) =>
        message.Contains("Products.Barcode", StringComparison.Ordinal) ? ProductFields.Barcode : ProductFields.Sku;
}
