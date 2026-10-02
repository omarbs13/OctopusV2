using Microsoft.EntityFrameworkCore;
using Pos.Application.Abstractions;
using Pos.Application.Products;
using Pos.Application.CashShifts;
using Pos.Domain.CashShifts;
using Pos.Application.Sales;
using Pos.Application.Sales.CancelSale;
using Pos.Domain.Inventory;
using Pos.Domain.Sales;
using Pos.Infrastructure.Sales;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Sales;

public sealed class CancelSaleTests : IAsyncLifetime
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
    public async Task Cancelling_restores_stock_with_matching_movements_and_writes_the_audit_entry()
    {
        var tracked = await SalesTestSupport.SeedProductAsync(_db, "CAN-1");
        var plain = await SalesTestSupport.SeedProductAsync(_db, "CAN-2", tracks: false);
        await SalesTestSupport.StockAsync(_db, tracked, "10");
        var sale = await SalesTestSupport.SellOkAsync(_db, (tracked, 4000), (plain, 1000));

        var result = await SalesTestSupport.CancelAsync(_db, sale.SaleId, "  Error de captura ");

        Assert.True(result.IsSuccess);
        await using var check = _db.CreateDbContext();
        var stored = await check.Sales.AsNoTracking().Include(s => s.Lines).SingleAsync(Ct);
        Assert.Equal(SaleStatus.Cancelled, stored.Status);
        Assert.Equal("Error de captura", stored.CancellationReason);
        Assert.NotNull(stored.CancelledAt);
        Assert.Equal(_db.User.UserId, stored.CancelledBy);
        Assert.Equal(2, stored.Version);

        Assert.Equal(10_000, (await check.ProductStocks.SingleAsync(Ct)).OnHandThousandths);
        var movements = await check.InventoryMovements.AsNoTracking().OrderBy(m => m.Sequence).ToListAsync(Ct);
        Assert.Equal(
            [MovementType.Initial, MovementType.Sale, MovementType.SaleCancellation],
            movements.Select(m => m.Type));
        Assert.Equal(movements[1].QuantityThousandths, movements[2].QuantityThousandths);
        Assert.Equal(sale.Folio, movements[2].Reference);
        Assert.Null(stored.Lines.Single(l => l.ProductId == plain.Id).CancellationMovementId);
        Assert.Equal(movements[2].Id, stored.Lines.Single(l => l.ProductId == tracked.Id).CancellationMovementId);

        var audit = await check.AuditEntries.AsNoTracking().SingleAsync(Ct);
        Assert.Equal("SALE_CANCELLED", audit.Action);
        Assert.Equal("Sale", audit.EntityType);
        Assert.Equal(sale.SaleId, audit.EntityId);
        Assert.Equal("Error de captura", audit.Reason);
        Assert.Equal($"Venta {sale.Folio}", audit.EntityName);
        Assert.Equal("Importe", audit.Changes[^1].Field);
    }

    [Fact]
    public async Task Cancelling_restores_stock_even_if_the_product_is_now_inactive_or_deleted()
    {
        var inactive = await SalesTestSupport.SeedProductAsync(_db, "CAN-3");
        var deleted = await SalesTestSupport.SeedProductAsync(_db, "CAN-4");
        await SalesTestSupport.StockAsync(_db, inactive, "10");
        await SalesTestSupport.StockAsync(_db, deleted, "10");
        var sale = await SalesTestSupport.SellOkAsync(_db, (inactive, 3000), (deleted, 2000));

        await using (var context = _db.CreateDbContext())
        {
            var first = await context.Products.SingleAsync(p => p.Id == inactive.Id, Ct);
            first.Update(first.Name, first.Sku, null, first.Price, "H87", isActive: false, true, null);
            var second = await context.Products.SingleAsync(p => p.Id == deleted.Id, Ct);
            second.Delete(_db.Clock.UtcNow);
            await context.SaveChangesAsync(Ct);
        }

        Assert.True((await SalesTestSupport.CancelAsync(_db, sale.SaleId)).IsSuccess);

        await using var check = _db.CreateDbContext();
        Assert.All(await check.ProductStocks.AsNoTracking().ToListAsync(Ct), s => Assert.Equal(10_000, s.OnHandThousandths));
    }

    [Fact]
    public async Task Cancelling_twice_returns_invalid_state_and_a_stale_version_returns_conflict()
    {
        var product = await SalesTestSupport.SeedProductAsync(_db, "CAN-5", tracks: false);
        var sale = await SalesTestSupport.SellOkAsync(_db, (product, 1000));

        Assert.IsType<Conflict>((await SalesTestSupport.CancelAsync(_db, sale.SaleId, expectedVersion: 99)).Error);
        Assert.True((await SalesTestSupport.CancelAsync(_db, sale.SaleId)).IsSuccess);
        var again = await SalesTestSupport.CancelAsync(_db, sale.SaleId);

        var invalid = Assert.IsType<InvalidState>(again.Error);
        Assert.Equal("Esta venta ya está cancelada", invalid.Message);
        await using var check = _db.CreateDbContext();
        Assert.Equal(1, await check.AuditEntries.CountAsync(Ct));
    }

    [Fact]
    public async Task Cancelling_without_reason_is_a_validation_error_and_unknown_sale_is_not_found()
    {
        var product = await SalesTestSupport.SeedProductAsync(_db, "CAN-6", tracks: false);
        var sale = await SalesTestSupport.SellOkAsync(_db, (product, 1000));

        Assert.IsType<ValidationFailed>((await SalesTestSupport.CancelAsync(_db, sale.SaleId, "   ")).Error);
        Assert.IsType<ValidationFailed>((await SalesTestSupport.CancelAsync(_db, sale.SaleId, new string('x', 251))).Error);
        Assert.IsType<NotFound>((await SalesTestSupport.CancelAsync(_db, Guid.CreateVersion7(), expectedVersion: 1)).Error);
    }

    [Fact]
    public async Task A_forced_failure_leaves_no_changes()
    {
        var product = await SalesTestSupport.SeedProductAsync(_db, "CAN-7");
        await SalesTestSupport.StockAsync(_db, product, "10");
        var sale = await SalesTestSupport.SellOkAsync(_db, (product, 4000));

        await using (var context = _db.CreateDbContext())
        {
            var failing = new FailAfterSave(new SaleRepository(context));
            await Assert.ThrowsAsync<InvalidOperationException>(() => SalesTestSupport.CancelHandler(_db, context, failing)
                .HandleAsync(new CancelSaleCommand(sale.SaleId, 1, "motivo"), Ct));
        }

        await using var check = _db.CreateDbContext();
        Assert.Equal(SaleStatus.Completed, (await check.Sales.SingleAsync(Ct)).Status);
        Assert.Equal(6000, (await check.ProductStocks.SingleAsync(Ct)).OnHandThousandths);
        Assert.Equal(2, await check.InventoryMovements.CountAsync(Ct));
        Assert.Equal(0, await check.AuditEntries.CountAsync(Ct));
    }

    [Fact]
    public async Task Audit_entries_cannot_be_modified_or_deleted()
    {
        var product = await SalesTestSupport.SeedProductAsync(_db, "CAN-8", tracks: false);
        var sale = await SalesTestSupport.SellOkAsync(_db, (product, 1000));
        await SalesTestSupport.CancelAsync(_db, sale.SaleId);

        await using (var context = _db.CreateDbContext())
        {
            var entry = await context.AuditEntries.SingleAsync(Ct);
            context.Entry(entry).Property(e => e.Details).CurrentValue = "otro";
            await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync(Ct));
        }

        await using (var context = _db.CreateDbContext())
        {
            context.AuditEntries.Remove(await context.AuditEntries.SingleAsync(Ct));
            await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync(Ct));
        }
    }

    private sealed class FailAfterSave(ISaleRepository inner) : ISaleRepository
    {
        public Task<bool> ExistsForDraftAsync(Guid draftId, CancellationToken ct) => inner.ExistsForDraftAsync(draftId, ct);

        public Task<ConfirmedSale?> FindByDraftAsync(Guid draftId, CancellationToken ct) => inner.FindByDraftAsync(draftId, ct);

        public Task<long> NextFolioNumberAsync(CancellationToken ct) => inner.NextFolioNumberAsync(ct);

        public Task<Sale?> GetAsync(Guid id, CancellationToken ct) => inner.GetAsync(id, ct);

        public void Add(Sale sale) => inner.Add(sale);

        public Task<SalePage> SearchAsync(SaleSearch search, CancellationToken ct) => inner.SearchAsync(search, ct);

        public Task<SaleDetailDto?> GetDetailAsync(Guid id, CancellationToken ct) => inner.GetDetailAsync(id, ct);

        public Task<ShiftSalesTotals> GetShiftTotalsAsync(Guid shiftId, CancellationToken ct) => inner.GetShiftTotalsAsync(shiftId, ct);

        public Task<IReadOnlyList<ShiftSaleRowDto>> ListByShiftAsync(Guid shiftId, CancellationToken ct) => inner.ListByShiftAsync(shiftId, ct);

        public Task<long> GetCashAppliedAsync(Guid saleId, CancellationToken ct) => inner.GetCashAppliedAsync(saleId, ct);

        public Task<SalesDashboard> GetDashboardAsync(IReadOnlyList<DayWindow> days, CancellationToken ct) =>
            inner.GetDashboardAsync(days, ct);

        public async Task<SaveOutcome> SaveChangesAsync(CancellationToken ct)
        {
            await inner.SaveChangesAsync(ct);
            throw new InvalidOperationException("Falla forzada antes de confirmar.");
        }
    }
}
