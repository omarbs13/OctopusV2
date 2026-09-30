using Microsoft.EntityFrameworkCore;
using Pos.Application.Abstractions;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Sales;

/// <summary>SC-005 y FR-020: folios consecutivos sin carreras y una sola venta por borrador.</summary>
public sealed class SaleFolioAndIdempotencyTests : IAsyncLifetime
{
    private TestDb _db = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _db = await TestDb.CreateAsync();

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Concurrent_confirmations_with_different_drafts_get_consecutive_folios()
    {
        var product = await SalesTestSupport.SeedProductAsync(_db, "FOL-1", tracks: false);

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(
            () => SalesTestSupport.SellAsync(_db, SalesTestSupport.CashSale(Guid.CreateVersion7(), (product, 1000))), Ct)));

        Assert.All(results, r => Assert.True(r.IsSuccess, r.Error?.ToString()));
        Assert.Equal(
            Enumerable.Range(1, 8).Select(n => $"V-{n:000000}"),
            results.Select(r => r.Value.Folio).Order(StringComparer.Ordinal));

        await using var check = _db.CreateDbContext();
        Assert.Equal(Enumerable.Range(1, 8).Select(n => (long)n), await check.Sales.OrderBy(s => s.FolioNumber).Select(s => s.FolioNumber).ToListAsync(Ct));
    }

    [Fact]
    public async Task Confirming_the_same_draft_twice_creates_one_sale_and_returns_the_same_folio()
    {
        var product = await SalesTestSupport.SeedProductAsync(_db, "FOL-2");
        await SalesTestSupport.StockAsync(_db, product, "10");
        var command = SalesTestSupport.CashSale(Guid.CreateVersion7(), (product, 2000));

        var first = await SalesTestSupport.SellAsync(_db, command);
        var second = await SalesTestSupport.SellAsync(_db, command);

        Assert.True(first.IsSuccess);
        var duplicate = Assert.IsType<AlreadyRegistered>(second.Error);
        Assert.Equal(first.Value.Folio, duplicate.Folio);
        Assert.Equal(first.Value.SaleId, duplicate.SaleId);

        await using var check = _db.CreateDbContext();
        Assert.Equal(1, await check.Sales.CountAsync(Ct));
        Assert.Equal(8000, (await check.ProductStocks.SingleAsync(Ct)).OnHandThousandths);
    }

    [Fact]
    public async Task Concurrent_confirmations_of_the_same_draft_register_only_once()
    {
        var product = await SalesTestSupport.SeedProductAsync(_db, "FOL-3");
        await SalesTestSupport.StockAsync(_db, product, "10");
        var command = SalesTestSupport.CashSale(Guid.CreateVersion7(), (product, 1000));

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Task.Run(
            () => SalesTestSupport.SellAsync(_db, command), Ct)));

        Assert.Equal(1, results.Count(r => r.IsSuccess));
        Assert.All(results.Where(r => !r.IsSuccess), r => Assert.IsType<AlreadyRegistered>(r.Error));

        await using var check = _db.CreateDbContext();
        Assert.Equal(1, await check.Sales.CountAsync(Ct));
        Assert.Equal(9000, (await check.ProductStocks.SingleAsync(Ct)).OnHandThousandths);
    }
}
