using Microsoft.EntityFrameworkCore;
using Pos.Application.Products;
using Pos.Application.CashShifts;
using Pos.Domain.CashShifts;
using Pos.Application.Sales;
using Pos.Domain.Sales;
using Pos.Infrastructure.Sales;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Sales;

/// <summary>SC-003 y FR-022: una falla antes de confirmar no deja nada a medias ni consume folio.</summary>
public sealed class SaleAtomicityTests : IAsyncLifetime
{
    private TestDb _db = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _db = await TestDb.CreateAsync();

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<int[]> CountsAsync()
    {
        await using var context = _db.CreateDbContext();
        return
        [
            await context.Sales.CountAsync(Ct),
            await context.SaleLines.CountAsync(Ct),
            await context.SalePayments.CountAsync(Ct),
            await context.InventoryMovements.CountAsync(Ct),
            await context.AuditEntries.CountAsync(Ct),
        ];
    }

    [Fact]
    public async Task Failure_before_commit_leaves_no_rows_and_keeps_the_draft_and_the_folio_is_reused()
    {
        var tracked = await SalesTestSupport.SeedProductAsync(_db, "ATO-1");
        var plain = await SalesTestSupport.SeedProductAsync(_db, "ATO-2", tracks: false);
        await SalesTestSupport.StockAsync(_db, tracked, "10");
        await SalesTestSupport.EnsureShiftAsync(_db);
        var draftId = Guid.CreateVersion7();

        await using (var context = _db.CreateDbContext())
        {
            await SalesTestSupport.SaveDraftHandler(_db, context).HandleAsync(
                new Pos.Application.Sales.SaveSaleDraft.SaveSaleDraftCommand(draftId, [new DraftLineDto(tracked.Id, 2000, 1000)]), Ct);
        }

        var before = await CountsAsync();
        var command = SalesTestSupport.CashSale(draftId, (tracked, 2000), (plain, 1000));

        await using (var context = _db.CreateDbContext())
        {
            var failing = new FailAfterSave(new SaleRepository(context));
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => SalesTestSupport.ConfirmHandler(_db, context, failing).HandleAsync(command, Ct));
        }

        Assert.Equal(before, await CountsAsync());
        await using (var check = _db.CreateDbContext())
        {
            Assert.Equal(draftId, (await check.SaleDrafts.SingleAsync(Ct)).DraftId);
            Assert.Equal(10_000, (await check.ProductStocks.SingleAsync(Ct)).OnHandThousandths);
        }

        var retry = await SalesTestSupport.SellAsync(_db, command);
        Assert.True(retry.IsSuccess);
        Assert.Equal("V-000001", retry.Value.Folio);

        await using var after = _db.CreateDbContext();
        Assert.Empty(await after.SaleDrafts.ToListAsync(Ct));
        Assert.Equal(8000, (await after.ProductStocks.SingleAsync(Ct)).OnHandThousandths);
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
