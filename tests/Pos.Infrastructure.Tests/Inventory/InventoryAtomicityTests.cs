using Microsoft.EntityFrameworkCore;
using Pos.Application.Inventory;
using Pos.Application.Inventory.RegisterMovement;
using Pos.Application.Products;
using Pos.Domain.Inventory;
using Pos.Infrastructure.Inventory;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Inventory;

/// <summary>Criterio de aceptación 1: una falla antes de confirmar deja sin cambios existencia y movimientos.</summary>
public sealed class InventoryAtomicityTests : IAsyncLifetime
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
    public async Task Failure_after_saving_and_before_commit_rolls_everything_back()
    {
        var product = await InventoryTestSupport.SeedProductAsync(_db, "ATO-1");

        await using (var context = _db.CreateDbContext())
        {
            var failing = new FailAfterSave(new InventoryRepository(context));
            await Assert.ThrowsAsync<InvalidOperationException>(() => InventoryTestSupport.Handler(context, failing)
                .HandleAsync(new RegisterMovementCommand(product.Id, MovementType.Initial, "10", null, null), Ct));
        }

        await using var check = _db.CreateDbContext();
        Assert.Equal(0, await check.ProductStocks.CountAsync(Ct));
        Assert.Equal(0, await check.InventoryMovements.CountAsync(Ct));
    }

    [Fact]
    public async Task Same_command_without_failure_saves_stock_and_movement_together()
    {
        var product = await InventoryTestSupport.SeedProductAsync(_db, "ATO-2");

        await using (var context = _db.CreateDbContext())
        {
            var result = await InventoryTestSupport.Handler(context)
                .HandleAsync(new RegisterMovementCommand(product.Id, MovementType.Initial, "10", null, "F-1"), Ct);
            Assert.True(result.IsSuccess);
            Assert.Equal(10_000, result.Value.ResultingStockThousandths);
            Assert.NotEqual(default, result.Value.CreatedAtUtc);
        }

        await using var check = _db.CreateDbContext();
        Assert.Equal(10_000, (await check.ProductStocks.SingleAsync(Ct)).OnHandThousandths);
        Assert.Equal("F-1", (await check.InventoryMovements.SingleAsync(Ct)).Reference);
    }

    private sealed class FailAfterSave(IInventoryRepository inner) : IInventoryRepository
    {
        public Task<ProductStock?> GetStockAsync(Guid productId, CancellationToken ct) => inner.GetStockAsync(productId, ct);

        public Task<IReadOnlyDictionary<Guid, ProductStock>> GetStocksAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct) =>
            inner.GetStocksAsync(productIds, ct);

        public Task<bool> HasMovementsAsync(Guid productId, CancellationToken ct) => inner.HasMovementsAsync(productId, ct);

        public void AddStock(ProductStock stock) => inner.AddStock(stock);

        public void AddMovement(InventoryMovement movement) => inner.AddMovement(movement);

        public async Task<SaveOutcome> SaveChangesAsync(CancellationToken ct)
        {
            await inner.SaveChangesAsync(ct);
            throw new InvalidOperationException("Falla forzada antes de confirmar.");
        }

        public Task<StockPage> SearchStockAsync(StockSearch search, CancellationToken ct) => inner.SearchStockAsync(search, ct);

        public Task<StockAlertCounts> CountAlertsAsync(CancellationToken ct) => inner.CountAlertsAsync(ct);

        public Task<MovementPage> SearchMovementsAsync(MovementSearch search, CancellationToken ct) => inner.SearchMovementsAsync(search, ct);
    }
}
