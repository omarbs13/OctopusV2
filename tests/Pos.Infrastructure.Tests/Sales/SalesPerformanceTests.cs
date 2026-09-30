using System.Diagnostics;
using Pos.Application.Sales;
using Pos.Application.Sales.FindProductsForSale;
using Pos.Application.Sales.SearchSales;
using Pos.Domain.Products;
using Pos.Domain.Sales;
using Pos.Infrastructure.Inventory;
using Pos.Infrastructure.Products;
using Pos.Infrastructure.Sales;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Sales;

/// <summary>
/// SC-002 y research §12: con 10,000 productos, confirmar una venta de 50 líneas tarda menos de 2 s y
/// buscar por código de barras exacto, menos de 100 ms; con 50,000 ventas, una página de 100 (con
/// filtros) tarda menos de 2 s.
/// </summary>
public sealed class SalesPerformanceTests : IAsyncLifetime
{
    private static readonly TimeSpan SaleBudget = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan LookupBudget = TimeSpan.FromMilliseconds(100);

    private TestDb _db = null!;
    private IReadOnlyList<Product> _products = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDb.CreateAsync();
        _products = await DatabaseTestHelpers.SeedProductsAsync(_db, 10_000, "SP");
        var file = _db.Directory.Paths.DatabaseFile;

        DatabaseTestHelpers.Execute(file, "UPDATE Products SET TracksInventory = 1, Barcode = printf('%013d', 7000000000000 + rowid)");
        DatabaseTestHelpers.Execute(file, """
            INSERT INTO ProductStocks (ProductId, OnHand, MovementCount, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, Version)
            SELECT Id, 100000, 1,
                   '2026-01-01 00:00:00.0000000', '00000000-0000-7000-8000-000000000001',
                   '2026-01-01 00:00:00.0000000', '00000000-0000-7000-8000-000000000001', 1
            FROM Products
            """);
        DatabaseTestHelpers.Execute(file, """
            INSERT INTO Sales (Id, FolioNumber, DraftId, TotalCents, Status, CancellationReason, CancelledAt, CancelledBy,
                               CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, Version)
            WITH RECURSIVE seq(n) AS (SELECT 1 UNION ALL SELECT n + 1 FROM seq WHERE n < 50000)
            SELECT lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-7' || substr(lower(hex(randomblob(2))), 2) || '-8' ||
                   substr(lower(hex(randomblob(2))), 2) || '-' || lower(hex(randomblob(6))),
                   n,
                   lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-7' || substr(lower(hex(randomblob(2))), 2) || '-8' ||
                   substr(lower(hex(randomblob(2))), 2) || '-' || lower(hex(randomblob(6))),
                   1000 + n,
                   CASE WHEN n % 10 = 0 THEN 'CANCELLED' ELSE 'COMPLETED' END,
                   CASE WHEN n % 10 = 0 THEN 'Prueba' ELSE NULL END,
                   NULL, NULL,
                   strftime('%Y-%m-%d %H:%M:%S', '2026-01-01', '+' || (n * 60) || ' seconds') || '.0000000',
                   '00000000-0000-7000-8000-000000000001',
                   strftime('%Y-%m-%d %H:%M:%S', '2026-01-01', '+' || (n * 60) || ' seconds') || '.0000000',
                   '00000000-0000-7000-8000-000000000001', 1
            FROM seq
            """);
        DatabaseTestHelpers.Execute(file, """
            INSERT INTO SalePayments (Id, SaleId, Method, AmountCents, ReceivedCents, ChangeCents, Reference)
            SELECT lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-7' || substr(lower(hex(randomblob(2))), 2) || '-8' ||
                   substr(lower(hex(randomblob(2))), 2) || '-' || lower(hex(randomblob(6))),
                   Id, CASE WHEN FolioNumber % 3 = 0 THEN 'CARD' ELSE 'CASH' END, TotalCents,
                   CASE WHEN FolioNumber % 3 = 0 THEN NULL ELSE TotalCents END,
                   CASE WHEN FolioNumber % 3 = 0 THEN NULL ELSE 0 END,
                   NULL
            FROM Sales
            """);

        // Calentamiento: la primera consulta compila el modelo de EF Core.
        await FindAsync("7000000000001");
        await SearchAsync(new SearchSalesQuery(null, null, null, null));
        Assert.True((await SalesTestSupport.SellAsync(_db, SalesTestSupport.CashSale(Guid.CreateVersion7(), (_products[0], 1000)))).IsSuccess);
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<ProductLookup> FindAsync(string code)
    {
        await using var context = _db.CreateDbContext();
        return (await new FindProductsForSaleHandler(new ProductRepository(context), new InventoryRepository(context))
            .HandleAsync(new FindProductsForSaleQuery(code), Ct)).Value;
    }

    private async Task<SalePage> SearchAsync(SearchSalesQuery query)
    {
        await using var context = _db.CreateDbContext();
        return (await new SearchSalesHandler(new SaleRepository(context)).HandleAsync(query, Ct)).Value;
    }

    private static async Task<(T Result, TimeSpan Elapsed)> MeasureAsync<T>(Func<Task<T>> action)
    {
        var watch = Stopwatch.StartNew();
        var result = await action();
        watch.Stop();
        return (result, watch.Elapsed);
    }

    [Fact]
    public async Task Confirming_a_sale_of_fifty_lines_is_fast()
    {
        var lines = _products.Skip(100).Take(50).Select(p => (p, 2000L)).ToArray();
        var command = SalesTestSupport.CashSale(Guid.CreateVersion7(), lines);

        var (result, elapsed) = await MeasureAsync(() => SalesTestSupport.SellAsync(_db, command));

        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.True(elapsed < SaleBudget, $"{elapsed.TotalMilliseconds:0} ms");
    }

    [Fact]
    public async Task Exact_barcode_lookup_is_fast()
    {
        var (lookup, elapsed) = await MeasureAsync(() => FindAsync("7000000005000"));

        Assert.Equal(LookupKind.ExactMatch, lookup.Kind);
        Assert.Equal("7000000005000", lookup.Items[0].Barcode);
        Assert.True(elapsed < LookupBudget, $"{elapsed.TotalMilliseconds:0} ms");
    }

    [Fact]
    public async Task A_page_of_one_hundred_sales_is_fast_with_and_without_filters()
    {
        var (all, allElapsed) = await MeasureAsync(() => SearchAsync(new SearchSalesQuery(null, null, null, null, 250)));
        var (filtered, filteredElapsed) = await MeasureAsync(() => SearchAsync(new SearchSalesQuery(
            new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc),
            null,
            SaleStatus.Completed)));
        var (byFolio, folioElapsed) = await MeasureAsync(() => SearchAsync(new SearchSalesQuery(null, null, "V-025000", null)));

        Assert.Equal(100, all.Items.Count);
        Assert.Equal(50_001, all.TotalCount);
        Assert.Equal(100, filtered.Items.Count);
        Assert.Equal("V-025000", Assert.Single(byFolio.Items).Folio);
        Assert.True(allElapsed < SaleBudget, $"Sin filtros: {allElapsed.TotalMilliseconds:0} ms");
        Assert.True(filteredElapsed < SaleBudget, $"Con filtros: {filteredElapsed.TotalMilliseconds:0} ms");
        Assert.True(folioElapsed < SaleBudget, $"Por folio: {folioElapsed.TotalMilliseconds:0} ms");
    }
}
