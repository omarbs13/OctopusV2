using Microsoft.EntityFrameworkCore;
using Pos.Application.Sales;
using Pos.Application.Sales.GetSaleDraft;
using Pos.Application.Sales.SaveSaleDraft;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Products;
using Pos.Infrastructure.Sales;
using Pos.Infrastructure.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Pos.Infrastructure.Tests.Sales;

public sealed class SaleDraftTests : IAsyncLifetime
{
    private TestDb _db = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _db = await TestDb.CreateAsync();

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task SaveAsync(Guid draftId, params DraftLineDto[] lines)
    {
        await using var context = _db.CreateDbContext();
        Assert.True((await SalesTestSupport.SaveDraftHandler(_db, context)
            .HandleAsync(new SaveSaleDraftCommand(draftId, lines), Ct)).IsSuccess);
    }

    [Fact]
    public async Task Saving_replaces_the_single_draft_row_and_it_can_be_recovered_with_current_product_data()
    {
        var product = await SalesTestSupport.SeedProductAsync(_db, "DRF-1", priceCents: 1500);

        await SaveAsync(Guid.CreateVersion7(), new DraftLineDto(product.Id, 1000, 1000));
        var second = Guid.CreateVersion7();
        await SaveAsync(second, new DraftLineDto(product.Id, 3000, 1000));

        await using var context = _db.CreateDbContext();
        Assert.Equal(1, await context.SaleDrafts.CountAsync(Ct));
        var recovered = (await new GetSaleDraftHandler(new AllowAllAccessControl(), 
                new SqliteSaleDraftStore(context, _db.Clock, _db.User),
                new ProductRepository(context),
                NullLogger<GetSaleDraftHandler>.Instance)
            .HandleAsync(Ct)).Value;
        Assert.NotNull(recovered);
        Assert.Equal(second, recovered.DraftId);
        var line = Assert.Single(recovered.Lines);
        Assert.Equal(3000, line.QuantityThousandths);
        Assert.Equal(1500, line.CurrentPriceCents);
        Assert.Null(line.NotSellableReason);
    }

    [Fact]
    public async Task Saving_without_lines_discards_the_draft()
    {
        var product = await SalesTestSupport.SeedProductAsync(_db, "DRF-2");
        var draftId = Guid.CreateVersion7();
        await SaveAsync(draftId, new DraftLineDto(product.Id, 1000, 1000));

        await SaveAsync(draftId);

        await using var context = _db.CreateDbContext();
        Assert.Empty(await context.SaleDrafts.ToListAsync(Ct));
    }

    [Fact]
    public async Task Confirming_removes_the_draft_and_a_late_save_with_the_same_id_does_not_revive_it()
    {
        var product = await SalesTestSupport.SeedProductAsync(_db, "DRF-3", tracks: false);
        var draftId = Guid.CreateVersion7();
        var line = new DraftLineDto(product.Id, 1000, 1000);
        await SaveAsync(draftId, line);

        var result = await SalesTestSupport.SellAsync(_db, SalesTestSupport.CashSale(draftId, (product, 1000)));
        Assert.True(result.IsSuccess);
        await using (var check = _db.CreateDbContext())
        {
            Assert.Empty(await check.SaleDrafts.ToListAsync(Ct));
        }

        await SaveAsync(draftId, line);

        await using var after = _db.CreateDbContext();
        Assert.Empty(await after.SaleDrafts.ToListAsync(Ct));
    }

    [Fact]
    public async Task A_draft_of_a_missing_product_is_omitted_on_recovery()
    {
        var product = await SalesTestSupport.SeedProductAsync(_db, "DRF-4");
        await SaveAsync(Guid.CreateVersion7(), new DraftLineDto(product.Id, 1000, 1000), new DraftLineDto(Guid.CreateVersion7(), 1000, 1000));

        await using var context = _db.CreateDbContext();
        var recovered = (await new GetSaleDraftHandler(new AllowAllAccessControl(), 
                new SqliteSaleDraftStore(context, _db.Clock, _db.User),
                new ProductRepository(context),
                NullLogger<GetSaleDraftHandler>.Instance)
            .HandleAsync(Ct)).Value;

        Assert.Equal([product.Id], recovered!.Lines.Select(l => l.ProductId));
    }

    [Fact]
    public async Task Saving_while_another_connection_holds_the_write_lock_waits_instead_of_failing()
    {
        var product = await SalesTestSupport.SeedProductAsync(_db, "DRF-5");
        await using var holder = _db.CreateDbContext();
        var transaction = await new WriteTransactions(holder).BeginAsync(Ct);

        var saving = Task.Run(() => SaveAsync(Guid.CreateVersion7(), new DraftLineDto(product.Id, 1000, 1000)), Ct);
        await Task.Delay(400, Ct);
        Assert.False(saving.IsCompleted);

        await transaction.CommitAsync(Ct);
        await transaction.DisposeAsync();
        await saving;

        await using var check = _db.CreateDbContext();
        Assert.Equal(1, await check.SaleDrafts.CountAsync(Ct));
    }
}
