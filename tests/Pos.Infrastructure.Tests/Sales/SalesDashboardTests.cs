using Pos.Application.Sales;
using Pos.Application.Sales.GetSalesDashboard;
using Pos.Application.Sales.SearchSales;
using Pos.Domain.Sales;
using Pos.Infrastructure.Sales;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Sales;

/// <summary>SC-009: las tarjetas de Inicio y "Ventas realizadas" cuentan lo mismo y excluyen las canceladas.</summary>
public sealed class SalesDashboardTests : IAsyncLifetime
{
    private TestDb _db = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateTime Utc(int day, int hour = 12) => new(2026, 9, day, hour, 0, 0, DateTimeKind.Utc);

    private static DayWindow Day(int day) => new(new DateOnly(2026, 9, day), Utc(day, 0), Utc(day + 1, 0));

    public async ValueTask InitializeAsync() => _db = await TestDb.CreateAsync();

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<SalesDashboard> DashboardAsync(params DayWindow[] days)
    {
        await using var context = _db.CreateDbContext();
        return (await new GetSalesDashboardHandler(new SaleRepository(context))
            .HandleAsync(new SalesDashboardQuery(days), Ct)).Value;
    }

    [Fact]
    public async Task Dashboard_excludes_cancelled_sales_fills_empty_days_and_ranks_by_quantity()
    {
        var a = await SalesTestSupport.SeedProductAsync(_db, "DSH-A", tracks: false, priceCents: 1000);
        var b = await SalesTestSupport.SeedProductAsync(_db, "DSH-B", "KGM", tracks: false, priceCents: 2000);

        _db.Clock.UtcNow = Utc(26);
        await SalesTestSupport.SellOkAsync(_db, (a, 1000));
        _db.Clock.UtcNow = Utc(27);
        await SalesTestSupport.SellOkAsync(_db, (a, 2000), (b, 1500));
        var cancelled = await SalesTestSupport.SellOkAsync(_db, (b, 10_000));
        Assert.True((await SalesTestSupport.CancelAsync(_db, cancelled.SaleId)).IsSuccess);
        _db.Clock.UtcNow = Utc(29);
        await SalesTestSupport.SellOkAsync(_db, (b, 500));

        var dashboard = await DashboardAsync(Day(26), Day(27), Day(28), Day(29));

        Assert.Equal([1000, 5000, 0, 1000], dashboard.Days.Select(d => d.TotalCents));
        Assert.Equal([1, 1, 0, 1], dashboard.Days.Select(d => d.Count));
        Assert.Equal(new DateOnly(2026, 9, 28), dashboard.Days[2].LocalDate);

        // A: 1 + 2 = 3 piezas; B: 1.5 + 0.5 = 2 kg (la venta cancelada de 10 kg no cuenta).
        Assert.Equal(["Producto DSH-A", "Producto DSH-B"], dashboard.TopProducts.Select(t => t.Name));
        Assert.Equal([3000L, 2000L], dashboard.TopProducts.Select(t => t.QuantityThousandths));
        Assert.Equal([0, 3], dashboard.TopProducts.Select(t => t.DecimalPlaces));
    }

    [Fact]
    public async Task Top_products_are_limited_to_five()
    {
        _db.Clock.UtcNow = Utc(28);
        for (var i = 1; i <= 7; i++)
        {
            var product = await SalesTestSupport.SeedProductAsync(_db, $"TOP-{i}", tracks: false);
            await SalesTestSupport.SellOkAsync(_db, (product, i * 1000L));
        }

        var dashboard = await DashboardAsync(Day(28));

        Assert.Equal(5, dashboard.TopProducts.Count);
        Assert.Equal([7000L, 6000L, 5000L, 4000L, 3000L], dashboard.TopProducts.Select(t => t.QuantityThousandths));
    }

    [Fact]
    public async Task Dashboard_matches_search_sales_for_the_same_range()
    {
        var product = await SalesTestSupport.SeedProductAsync(_db, "DSH-C", tracks: false, priceCents: 1234);
        _db.Clock.UtcNow = Utc(27, 1);
        await SalesTestSupport.SellOkAsync(_db, (product, 1000));
        _db.Clock.UtcNow = Utc(27, 23);
        await SalesTestSupport.SellOkAsync(_db, (product, 3000));
        var cancelled = await SalesTestSupport.SellOkAsync(_db, (product, 2000));
        await SalesTestSupport.CancelAsync(_db, cancelled.SaleId);
        _db.Clock.UtcNow = Utc(28, 0);
        await SalesTestSupport.SellOkAsync(_db, (product, 1000));

        var day = Day(27);
        var dashboard = await DashboardAsync(day);

        await using var context = _db.CreateDbContext();
        var page = (await new SearchSalesHandler(new SaleRepository(context)).HandleAsync(
            new SearchSalesQuery(day.FromUtc, day.ToUtcExclusive, null, SaleStatus.Completed), Ct)).Value;
        Assert.Equal(page.TotalCount, dashboard.Days[0].Count);
        Assert.Equal(page.Items.Sum(i => i.TotalCents), dashboard.Days[0].TotalCents);
        Assert.Equal(2, page.TotalCount);
    }
}
