using Pos.Application.Abstractions;
using Pos.Application.Sales.GetSale;
using Pos.Application.Sales.SearchSales;
using Pos.Domain.Sales;
using Pos.Infrastructure.Sales;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Sales;

public sealed class SearchSalesTests : IAsyncLifetime
{
    private TestDb _db = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateTime Utc(int day, int hour = 12) => new(2026, 9, day, hour, 0, 0, DateTimeKind.Utc);

    public async ValueTask InitializeAsync() => _db = await TestDb.CreateAsync();

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<Result<Pos.Application.Sales.SalePage>> SearchAsync(SearchSalesQuery query)
    {
        await using var context = _db.CreateDbContext();
        return await new SearchSalesHandler(new AllowAllAccessControl(), _db.User, new SaleRepository(context)).HandleAsync(query, Ct);
    }

    private async Task SeedAsync()
    {
        var product = await SalesTestSupport.SeedProductAsync(_db, "SRC-1", tracks: false, priceCents: 1000);
        for (var day = 1; day <= 3; day++)
        {
            _db.Clock.UtcNow = Utc(day);
            await SalesTestSupport.SellOkAsync(_db, (product, 1000));
        }

        var last = await SalesTestSupport.SellOkAsync(_db, (product, 2000));
        await SalesTestSupport.CancelAsync(_db, last.SaleId);
    }

    [Fact]
    public async Task Sales_are_listed_from_newest_to_oldest_with_their_payment_methods()
    {
        await SeedAsync();

        var page = (await SearchAsync(new SearchSalesQuery(null, null, null, null))).Value;

        Assert.Equal(4, page.TotalCount);
        Assert.Equal(["V-000004", "V-000003", "V-000002", "V-000001"], page.Items.Select(i => i.Folio));
        Assert.All(page.Items, i => Assert.Equal([PaymentMethod.Cash], i.Methods));
        Assert.Equal(SaleStatus.Cancelled, page.Items[0].Status);
    }

    [Fact]
    public async Task Filters_by_range_folio_and_status()
    {
        await SeedAsync();

        var range = (await SearchAsync(new SearchSalesQuery(Utc(2, 0), Utc(3, 0), null, null))).Value;
        var folio = (await SearchAsync(new SearchSalesQuery(null, null, "v2", null))).Value;
        var cancelled = (await SearchAsync(new SearchSalesQuery(null, null, null, SaleStatus.Cancelled))).Value;

        Assert.Equal(["V-000002"], range.Items.Select(i => i.Folio));
        Assert.Equal(["V-000002"], folio.Items.Select(i => i.Folio));
        Assert.Equal(["V-000004"], cancelled.Items.Select(i => i.Folio));
    }

    [Fact]
    public async Task Invalid_folio_or_inverted_range_is_a_validation_error()
    {
        Assert.IsType<ValidationFailed>((await SearchAsync(new SearchSalesQuery(null, null, "abc", null))).Error);
        Assert.IsType<ValidationFailed>((await SearchAsync(new SearchSalesQuery(Utc(3), Utc(1), null, null))).Error);
    }

    [Fact]
    public async Task Detail_returns_the_saved_values_and_not_found_for_unknown_ids()
    {
        var product = await SalesTestSupport.SeedProductAsync(_db, "SRC-2", tracks: false, priceCents: 1050);
        var sale = await SalesTestSupport.SellOkAsync(_db, (product, 2000));

        await using var context = _db.CreateDbContext();
        var handler = new GetSaleHandler(new AllowAllAccessControl(), _db.User, new SaleRepository(context));
        var detail = (await handler.HandleAsync(new GetSaleQuery(sale.SaleId), Ct)).Value;

        Assert.Equal(sale.Folio, detail.Folio);
        Assert.Equal(1, detail.Version);
        var line = Assert.Single(detail.Lines);
        Assert.Equal((1050, 2000, 2100), (line.UnitPriceCents, line.QuantityThousandths, line.AmountCents));
        var payment = Assert.Single(detail.Payments);
        Assert.Equal(PaymentMethod.Cash, payment.Method);
        Assert.Equal(2100, payment.ReceivedCents);
        Assert.Equal(0, payment.ChangeCents);
        Assert.IsType<NotFound>((await handler.HandleAsync(new GetSaleQuery(Guid.CreateVersion7()), Ct)).Error);
    }
}
