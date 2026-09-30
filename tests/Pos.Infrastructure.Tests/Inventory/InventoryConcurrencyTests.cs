using Microsoft.EntityFrameworkCore;
using Pos.Application.Abstractions;
using Pos.Application.Inventory.RegisterMovement;
using Pos.Domain.Inventory;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Inventory;

/// <summary>Research §5: los escritores se serializan y la existencia nunca queda negativa.</summary>
public sealed class InventoryConcurrencyTests : IAsyncLifetime
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
    public async Task Concurrent_negative_adjustments_only_succeed_while_stock_lasts()
    {
        var product = await InventoryTestSupport.SeedProductAsync(_db, "CON-1");
        await using (var context = _db.CreateDbContext())
        {
            var start = await InventoryTestSupport.Handler(context)
                .HandleAsync(new RegisterMovementCommand(product.Id, MovementType.Initial, "5", null, null), Ct);
            Assert.True(start.IsSuccess);
        }

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            await using var context = _db.CreateDbContext();
            return await InventoryTestSupport.Handler(context)
                .HandleAsync(new RegisterMovementCommand(product.Id, MovementType.AdjustOut, "1", "venta", null), Ct);
        }, Ct)));

        Assert.Equal(5, results.Count(r => r.IsSuccess));
        Assert.All(results.Where(r => !r.IsSuccess), r => Assert.IsType<ValidationFailed>(r.Error));

        await using var check = _db.CreateDbContext();
        var stock = await check.ProductStocks.SingleAsync(Ct);
        Assert.Equal(0, stock.OnHandThousandths);
        Assert.Equal(6, stock.MovementCount);
        Assert.Equal(6, await check.InventoryMovements.CountAsync(Ct));
    }
}
