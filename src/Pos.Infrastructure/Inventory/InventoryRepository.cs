using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Inventory;
using Pos.Application.Products;
using Pos.Domain.Common;
using Pos.Domain.Inventory;
using Pos.Domain.Products;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Products;

namespace Pos.Infrastructure.Inventory;

public sealed class InventoryRepository : IInventoryRepository
{
    // https://www.sqlite.org/rescode.html#constraint_primarykey y #constraint_unique
    private const int SqliteConstraintPrimaryKey = 1555;
    private const int SqliteConstraintUnique = 2067;

    private readonly PosDbContext _context;

    public InventoryRepository(PosDbContext context) => _context = context;

    public Task<ProductStock?> GetStockAsync(Guid productId, CancellationToken cancellationToken) =>
        _context.ProductStocks.SingleOrDefaultAsync(s => s.ProductId == productId, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, ProductStock>> GetStocksAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(productIds);
        var stocks = await _context.ProductStocks
            .Where(s => productIds.Contains(s.ProductId))
            .ToListAsync(cancellationToken);
        return stocks.ToDictionary(s => s.ProductId);
    }

    public Task<bool> HasMovementsAsync(Guid productId, CancellationToken cancellationToken) =>
        _context.ProductStocks.AnyAsync(s => s.ProductId == productId, cancellationToken);

    public void AddStock(ProductStock stock) => _context.ProductStocks.Add(stock);

    public void AddMovement(InventoryMovement movement) => _context.InventoryMovements.Add(movement);

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

    public async Task<StockPage> SearchStockAsync(StockSearch search, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(search);

        var rows = FilterByStatus(StockRows(search.IncludeInactive, search), search.Filter);
        var total = await rows.LongCountAsync(cancellationToken);
        var page = Math.Clamp(search.Page, 1, ProductPage.PageCount(total, search.PageSize));

        var items = await rows
            .Join(_context.UnitsOfMeasure, r => r.Product.UnitCode, u => u.Code, (r, u) => new { Row = r, Unit = u })
            .OrderBy(x => x.Row.Product.NameSearch)
            .ThenBy(x => x.Row.Product.Sku)
            .Skip((page - 1) * search.PageSize)
            .Take(search.PageSize)
            .Select(x => new
            {
                x.Row.Product.Id,
                x.Row.Product.Name,
                x.Row.Product.Sku,
                x.Row.Product.Barcode,
                UnitCode = x.Unit.Code,
                UnitName = x.Unit.Name,
                x.Unit.DecimalPlaces,
                x.Row.OnHand,
                Minimum = x.Row.Product.MinimumStockThousandths,
                x.Row.Product.IsActive,
                x.Row.HasMovements,
            })
            .ToListAsync(cancellationToken);

        var dtos = items
            .Select(i => new StockItemDto(
                i.Id,
                i.Name,
                i.Sku,
                i.Barcode,
                i.UnitCode,
                i.UnitName,
                i.DecimalPlaces,
                i.OnHand,
                i.Minimum,
                StockStatusRule.Evaluate(
                    StockLevel.FromThousandths(i.OnHand),
                    i.Minimum is { } m ? Quantity.FromThousandths(m) : null),
                i.IsActive,
                i.HasMovements))
            .ToList();
        return new StockPage(dtos, total, page, search.PageSize);
    }

    public async Task<StockAlertCounts> CountAlertsAsync(CancellationToken cancellationToken)
    {
        var rows = StockRows(includeInactive: false, search: null);
        var low = await FilterByStatus(rows, StockFilter.Low).LongCountAsync(cancellationToken);
        var outOfStock = await FilterByStatus(rows, StockFilter.Out).LongCountAsync(cancellationToken);
        return new StockAlertCounts(low, outOfStock);
    }

    public async Task<MovementPage> SearchMovementsAsync(MovementSearch search, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(search);

        var movements = _context.InventoryMovements.AsNoTracking().AsQueryable();
        if (search.ProductId is { } productId)
        {
            movements = movements.Where(m => m.ProductId == productId);
        }

        if (search.Type is { } type)
        {
            movements = movements.Where(m => m.Type == type);
        }

        if (search.FromUtc is { } from)
        {
            movements = movements.Where(m => m.CreatedAt >= from);
        }

        if (search.ToUtcExclusive is { } to)
        {
            movements = movements.Where(m => m.CreatedAt < to);
        }

        var total = await movements.LongCountAsync(cancellationToken);
        var page = Math.Clamp(search.Page, 1, ProductPage.PageCount(total, search.PageSize));

        var joined =
            from m in movements
            join p in _context.Products on m.ProductId equals p.Id
            join u in _context.UnitsOfMeasure on p.UnitCode equals u.Code
            join author in _context.Users on m.CreatedBy equals author.Id into authors
            from author in authors.DefaultIfEmpty()
            select new { Movement = m, Product = p, Unit = u, AuthorName = author == null ? null : author.FullName };

        // El kárdex de un producto se ordena por su secuencia, que es exacta; el historial general,
        // por fecha (research §6).
        var ordered = search.ProductId is null
            ? joined.OrderByDescending(x => x.Movement.CreatedAt).ThenByDescending(x => x.Movement.Id)
            : joined.OrderByDescending(x => x.Movement.Sequence);

        var items = await ordered
            .Skip((page - 1) * search.PageSize)
            .Take(search.PageSize)
            .Select(x => new MovementDto(
                x.Movement.Id,
                x.Movement.CreatedAt,
                x.Movement.ProductId,
                x.Product.Name,
                x.Product.Sku,
                x.Unit.Name,
                x.Unit.DecimalPlaces,
                x.Movement.Type,
                x.Movement.QuantityThousandths,
                x.Movement.ResultingStockThousandths,
                x.Movement.Reason,
                x.Movement.Reference,
                x.Movement.CreatedBy,
                x.AuthorName ?? string.Empty))
            .ToListAsync(cancellationToken);

        return new MovementPage(items, total, page, search.PageSize);
    }

    /// <summary>
    /// Productos no borrados que controlan inventario, con su existencia (0 si no tienen fila). Es la
    /// base común del listado y de los conteos, para que coincidan (SC-006).
    /// </summary>
    private IQueryable<StockRow> StockRows(bool includeInactive, StockSearch? search)
    {
        var products = _context.Products.AsNoTracking().Where(p => p.DeletedAt == null && p.TracksInventory);
        if (!includeInactive)
        {
            products = products.Where(p => p.IsActive);
        }

        if (search is not null)
        {
            products = ProductTextFilter.Apply(products, search.NameText, search.SkuText, search.BarcodeText, search.BarcodeExact);
        }

        return from p in products
               join s in _context.ProductStocks on p.Id equals s.ProductId into stocks
               from s in stocks.DefaultIfEmpty()
               select new StockRow
               {
                   Product = p,
                   OnHand = s == null ? 0L : s.OnHandThousandths,
                   HasMovements = s != null,
               };
    }

    /// <summary>Predicado de estado en SQL; replica <see cref="StockStatusRule"/> (research §8).</summary>
    private static IQueryable<StockRow> FilterByStatus(IQueryable<StockRow> rows, StockFilter filter) => filter switch
    {
        StockFilter.Out => rows.Where(r => r.OnHand <= 0),
        StockFilter.Low => rows.Where(r => r.OnHand > 0
            && r.Product.MinimumStockThousandths != null
            && r.OnHand <= r.Product.MinimumStockThousandths),
        StockFilter.Normal => rows.Where(r => r.OnHand > 0
            && (r.Product.MinimumStockThousandths == null || r.OnHand > r.Product.MinimumStockThousandths)),
        _ => rows,
    };

    private sealed class StockRow
    {
        public Product Product { get; set; } = null!;

        public long OnHand { get; set; }

        public bool HasMovements { get; set; }
    }
}
