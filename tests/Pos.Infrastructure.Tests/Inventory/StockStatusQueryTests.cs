using Pos.Application.Inventory;
using Pos.Application.Inventory.RegisterMovement;
using Pos.Application.Inventory.SearchStock;
using Pos.Domain.Common;
using Pos.Domain.Inventory;
using Pos.Infrastructure.Inventory;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Inventory;

/// <summary>
/// SC-006 y research §8: el filtro y el conteo en SQL coinciden con <see cref="StockStatusRule"/>
/// en los casos frontera.
/// </summary>
public sealed class StockStatusQueryTests : IAsyncLifetime
{
    private TestDb _db = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDb.CreateAsync();

        await SeedAsync("ST-LOW", minimum: 5000, initial: "5");
        await SeedAsync("ST-OUT-MIN", minimum: 5000, initial: "5", adjustOutAll: true);
        await SeedAsync("ST-OUT-NOROW", minimum: 5000, initial: null);
        await SeedAsync("ST-NORMAL-NOMIN", minimum: null, initial: "3");
        await SeedAsync("ST-NORMAL-ABOVE", minimum: 5000, initial: "6");
        await SeedAsync("ST-OUT-INACTIVE", minimum: null, initial: null, active: false);
        await SeedAsync("ST-NOTRACK", minimum: null, initial: null, tracks: false);
        await SeedNegativeAsync("ST-NEG", minimum: 5000);
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task SeedAsync(
        string sku,
        long? minimum,
        string? initial,
        bool active = true,
        bool tracks = true,
        bool adjustOutAll = false)
    {
        var product = await InventoryTestSupport.SeedProductAsync(_db, sku, "H87", tracks, minimum);
        if (initial is not null)
        {
            await using var context = _db.CreateDbContext();
            var handler = InventoryTestSupport.Handler(context);
            Assert.True((await handler.HandleAsync(new RegisterMovementCommand(product.Id, MovementType.Initial, initial, null, null), Ct)).IsSuccess);
            if (adjustOutAll)
            {
                Assert.True((await handler.HandleAsync(new RegisterMovementCommand(product.Id, MovementType.AdjustOut, initial, "merma", null), Ct)).IsSuccess);
            }
        }

        if (!active)
        {
            await using var context = _db.CreateDbContext();
            var tracked = await context.Products.FindAsync([product.Id], Ct);
            tracked!.Update(tracked.Name, tracked.Sku, null, tracked.Price, "H87", isActive: false, tracked.TracksInventory, tracked.MinimumStock);
            await context.SaveChangesAsync(Ct);
        }
    }

    /// <summary>Existencia 3 y una venta de 5: queda en -2.</summary>
    private async Task SeedNegativeAsync(string sku, long minimum)
    {
        var product = await InventoryTestSupport.SeedProductAsync(_db, sku, "H87", true, minimum);
        await SalesTestSupport.StockAsync(_db, product, "3");
        await SalesTestSupport.SellOkAsync(_db, (product, 5000));
    }

    private async Task<StockPage> SearchAsync(StockFilter filter, bool includeInactive = false, string? text = null)
    {
        await using var context = _db.CreateDbContext();
        return (await new SearchStockHandler(new AllowAllAccessControl(), new InventoryRepository(context))
            .HandleAsync(new SearchStockQuery(text, filter, includeInactive), Ct)).Value;
    }

    [Theory]
    [InlineData(StockFilter.Low, new[] { "ST-LOW" })]
    [InlineData(StockFilter.Out, new[] { "ST-NEG", "ST-OUT-MIN", "ST-OUT-NOROW" })]
    [InlineData(StockFilter.Normal, new[] { "ST-NORMAL-ABOVE", "ST-NORMAL-NOMIN" })]
    [InlineData(StockFilter.All, new[] { "ST-LOW", "ST-NEG", "ST-NORMAL-ABOVE", "ST-NORMAL-NOMIN", "ST-OUT-MIN", "ST-OUT-NOROW" })]
    public async Task Filter_returns_only_the_products_in_that_status(StockFilter filter, string[] expectedSkus)
    {
        var page = await SearchAsync(filter);

        Assert.Equal(expectedSkus.Order(StringComparer.Ordinal), page.Items.Select(i => i.Sku).Order(StringComparer.Ordinal));
        Assert.Equal(expectedSkus.Length, page.TotalCount);
    }

    [Fact]
    public async Task Sql_status_matches_the_domain_rule_for_every_row()
    {
        var page = await SearchAsync(StockFilter.All, includeInactive: true);

        Assert.Equal(7, page.TotalCount);
        foreach (var item in page.Items)
        {
            var expected = StockStatusRule.Evaluate(
                StockLevel.FromThousandths(item.OnHandThousandths),
                item.MinimumThousandths is { } m ? Quantity.FromThousandths(m) : null);
            Assert.Equal(expected, item.Status);

            var filtered = await SearchAsync(ToFilter(expected), includeInactive: true);
            Assert.Contains(filtered.Items, i => i.ProductId == item.ProductId);
        }
    }

    [Fact]
    public async Task Alert_counts_use_the_same_predicate_and_skip_inactive_products()
    {
        await using var context = _db.CreateDbContext();
        var counts = await new InventoryRepository(context).CountAlertsAsync(Ct);

        Assert.Equal(1, counts.Low);
        Assert.Equal(3, counts.Out);
        Assert.Equal((await SearchAsync(StockFilter.Low)).TotalCount, counts.Low);
        Assert.Equal((await SearchAsync(StockFilter.Out)).TotalCount, counts.Out);

        var withInactive = await SearchAsync(StockFilter.Out, includeInactive: true);
        Assert.Equal(4, withInactive.TotalCount);
        Assert.False(withInactive.Items.Single(i => i.Sku == "ST-OUT-INACTIVE").IsActive);
    }

    [Fact]
    public async Task Negative_stock_is_out_of_stock_in_sql_and_in_the_domain_rule()
    {
        var page = await SearchAsync(StockFilter.Out);

        var negative = page.Items.Single(i => i.Sku == "ST-NEG");
        Assert.Equal(-2000, negative.OnHandThousandths);
        Assert.Equal(StockStatus.Out, negative.Status);
        Assert.DoesNotContain((await SearchAsync(StockFilter.Low)).Items, i => i.Sku == "ST-NEG");
    }

    [Fact]
    public async Task Search_text_matches_name_and_sku()
    {
        var page = await SearchAsync(StockFilter.All, text: "st-low");

        Assert.Equal(["ST-LOW"], page.Items.Select(i => i.Sku));
    }

    private static StockFilter ToFilter(StockStatus status) => status switch
    {
        StockStatus.Out => StockFilter.Out,
        StockStatus.Low => StockFilter.Low,
        _ => StockFilter.Normal,
    };
}
