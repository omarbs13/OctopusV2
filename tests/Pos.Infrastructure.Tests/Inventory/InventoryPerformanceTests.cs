using System.Diagnostics;
using Pos.Application.Inventory;
using Pos.Application.Inventory.SearchMovements;
using Pos.Application.Inventory.SearchStock;
using Pos.Domain.Inventory;
using Pos.Infrastructure.Inventory;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Inventory;

/// <summary>
/// SC-005: con 10,000 productos y 100,000 movimientos, cada página de 100 de Existencias y de
/// Movimientos, con y sin filtros, tarda menos de 2 segundos.
/// </summary>
public sealed class InventoryPerformanceTests : IAsyncLifetime
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(2);

    private TestDb _db = null!;
    private Guid _someProduct;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDb.CreateAsync();
        await DatabaseTestHelpers.SeedProductsAsync(_db, 10_000, "PF");
        var file = _db.Directory.Paths.DatabaseFile;

        DatabaseTestHelpers.Execute(file, "UPDATE Products SET TracksInventory = 1, MinimumStock = CASE WHEN rowid % 3 = 0 THEN 10000 ELSE NULL END");
        DatabaseTestHelpers.Execute(file, """
            INSERT INTO ProductStocks (ProductId, OnHand, MovementCount, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, Version)
            SELECT Id, CASE WHEN rowid % 5 = 0 THEN 0 ELSE 10000 END, 10,
                   '2026-01-01 00:00:00.0000000', '00000000-0000-7000-8000-000000000001',
                   '2026-01-01 00:00:00.0000000', '00000000-0000-7000-8000-000000000001', 1
            FROM Products
            """);
        DatabaseTestHelpers.Execute(file, """
            INSERT INTO InventoryMovements (Id, ProductId, Sequence, Type, Quantity, ResultingStock, Reason, Reference, CreatedAt, CreatedBy)
            WITH RECURSIVE seq(n) AS (SELECT 1 UNION ALL SELECT n + 1 FROM seq WHERE n < 10)
            SELECT lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-7' || substr(lower(hex(randomblob(2))), 2) || '-8' ||
                   substr(lower(hex(randomblob(2))), 2) || '-' || lower(hex(randomblob(6))),
                   p.Id, s.n, CASE WHEN s.n = 1 THEN 'INITIAL' ELSE 'RECEIPT' END, 1000, s.n * 1000, NULL, NULL,
                   strftime('%Y-%m-%d %H:%M:%S', '2026-01-01', '+' || (p.rowid * 10 + s.n) || ' seconds') || '.0000000',
                   '00000000-0000-7000-8000-000000000001'
            FROM Products p CROSS JOIN seq s
            """);

        await using var context = _db.CreateDbContext();
        _someProduct = context.Products.OrderBy(p => p.Sku).Skip(500).Select(p => p.Id).First();

        // Calentamiento: la primera consulta compila el modelo de EF Core.
        await StockAsync(StockFilter.All);
        await MovementsAsync(new SearchMovementsQuery(null, null, null, null));
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<StockPage> StockAsync(StockFilter filter, string? text = null, int page = 1)
    {
        await using var context = _db.CreateDbContext();
        return (await new SearchStockHandler(new InventoryRepository(context))
            .HandleAsync(new SearchStockQuery(text, filter, IncludeInactive: true, page), Ct)).Value;
    }

    private async Task<MovementPage> MovementsAsync(SearchMovementsQuery query)
    {
        await using var context = _db.CreateDbContext();
        return (await new SearchMovementsHandler(new InventoryRepository(context)).HandleAsync(query, Ct)).Value;
    }

    private static async Task<(T Result, TimeSpan Elapsed)> MeasureAsync<T>(Func<Task<T>> action)
    {
        var watch = Stopwatch.StartNew();
        var result = await action();
        watch.Stop();
        return (result, watch.Elapsed);
    }

    [Theory]
    [InlineData(StockFilter.All)]
    [InlineData(StockFilter.Low)]
    [InlineData(StockFilter.Out)]
    [InlineData(StockFilter.Normal)]
    public async Task Stock_page_with_status_filter_is_fast(StockFilter filter)
    {
        var (page, elapsed) = await MeasureAsync(() => StockAsync(filter));

        Assert.Equal(100, page.Items.Count);
        Assert.True(elapsed < Budget, $"{filter}: {elapsed.TotalMilliseconds:0} ms");
    }

    [Fact]
    public async Task Stock_last_page_and_text_search_are_fast()
    {
        var (last, lastElapsed) = await MeasureAsync(() => StockAsync(StockFilter.All, page: 100));
        var (found, searchElapsed) = await MeasureAsync(() => StockAsync(StockFilter.All, "pf-9999"));

        Assert.Equal(100, last.Page);
        Assert.Single(found.Items);
        Assert.True(lastElapsed < Budget, $"Última página: {lastElapsed.TotalMilliseconds:0} ms");
        Assert.True(searchElapsed < Budget, $"Búsqueda: {searchElapsed.TotalMilliseconds:0} ms");
    }

    [Fact]
    public async Task Movements_page_without_filters_is_fast()
    {
        var (page, elapsed) = await MeasureAsync(() => MovementsAsync(new SearchMovementsQuery(null, null, null, null)));

        Assert.Equal(100, page.Items.Count);
        Assert.Equal(100_000, page.TotalCount);
        Assert.True(elapsed < Budget, $"{elapsed.TotalMilliseconds:0} ms");
    }

    [Fact]
    public async Task Movements_with_product_type_and_date_filters_are_fast()
    {
        var (byProduct, productElapsed) = await MeasureAsync(() =>
            MovementsAsync(new SearchMovementsQuery(_someProduct, null, null, null)));
        var (byType, typeElapsed) = await MeasureAsync(() =>
            MovementsAsync(new SearchMovementsQuery(null, MovementType.Initial, null, null, 50)));
        var (byDate, dateElapsed) = await MeasureAsync(() =>
            MovementsAsync(new SearchMovementsQuery(
                null,
                null,
                new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc))));

        Assert.Equal(10, byProduct.Items.Count);
        Assert.Equal(10_000, byType.TotalCount);
        Assert.NotEmpty(byDate.Items);
        Assert.True(productElapsed < Budget, $"Por producto: {productElapsed.TotalMilliseconds:0} ms");
        Assert.True(typeElapsed < Budget, $"Por tipo: {typeElapsed.TotalMilliseconds:0} ms");
        Assert.True(dateElapsed < Budget, $"Por fecha: {dateElapsed.TotalMilliseconds:0} ms");
    }
}
