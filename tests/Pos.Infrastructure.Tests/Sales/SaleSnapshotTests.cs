using Microsoft.EntityFrameworkCore;
using Pos.Application.Abstractions;
using Pos.Domain.Common;
using Pos.Domain.Inventory;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Sales;

public sealed class SaleSnapshotTests : IAsyncLifetime
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
    public async Task Changing_the_product_after_selling_does_not_alter_the_sale_lines()
    {
        var product = await SalesTestSupport.SeedProductAsync(_db, "SNP-1", priceCents: 1050);
        await SalesTestSupport.StockAsync(_db, product, "5");
        var sale = await SalesTestSupport.SellOkAsync(_db, (product, 2000));

        await using (var context = _db.CreateDbContext())
        {
            var tracked = await context.Products.SingleAsync(p => p.Id == product.Id, Ct);
            tracked.Update("Otro nombre", "OTRO-SKU", null, Money.FromCents(9999), "H87", isActive: true, tracksInventory: true, minimumStock: null);
            await context.SaveChangesAsync(Ct);
        }

        await using var check = _db.CreateDbContext();
        var line = await check.SaleLines.AsNoTracking().SingleAsync(Ct);
        Assert.Equal("Producto SNP-1", line.ProductName);
        Assert.Equal("SNP-1", line.ProductSku);
        Assert.Equal(1050, line.UnitPriceCents);
        Assert.Equal(2100, line.AmountCents);
        Assert.Equal(sale.SaleId, line.SaleId);
    }

    [Fact]
    public async Task A_product_without_inventory_control_creates_no_movement()
    {
        var tracked = await SalesTestSupport.SeedProductAsync(_db, "SNP-2");
        var service = await SalesTestSupport.SeedProductAsync(_db, "SNP-3", tracks: false, priceCents: 5000);
        await SalesTestSupport.StockAsync(_db, tracked, "5");
        _db.User.UserId = SystemUser.Id;

        await SalesTestSupport.SellOkAsync(_db, (tracked, 1000), (service, 1000));

        await using var check = _db.CreateDbContext();
        var lines = await check.SaleLines.AsNoTracking().OrderBy(l => l.Position).ToListAsync(Ct);
        Assert.NotNull(lines[0].SaleMovementId);
        Assert.Null(lines[1].SaleMovementId);
        Assert.Equal(1, await check.InventoryMovements.CountAsync(m => m.Type == MovementType.Sale, Ct));
        Assert.Empty(await check.ProductStocks.Where(s => s.ProductId == service.Id).ToListAsync(Ct));
    }

    [Fact]
    public async Task Sale_and_movements_are_created_by_the_system_user()
    {
        var product = await SalesTestSupport.SeedProductAsync(_db, "SNP-4");
        await SalesTestSupport.StockAsync(_db, product, "5");
        _db.User.UserId = SystemUser.Id;

        await SalesTestSupport.SellOkAsync(_db, (product, 1000));

        await using var check = _db.CreateDbContext();
        Assert.Equal(SystemUser.Id, (await check.Sales.SingleAsync(Ct)).CreatedBy);
        Assert.All(
            await check.InventoryMovements.Where(m => m.Type == MovementType.Sale).ToListAsync(Ct),
            m => Assert.Equal(SystemUser.Id, m.CreatedBy));
    }
}
