using Microsoft.EntityFrameworkCore;
using Pos.Domain.CashShifts;
using Pos.Domain.Common;
using Pos.Infrastructure.CashShifts;
using Pos.Infrastructure.Sales;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.CashShifts;

/// <summary>Persistencia de turnos sobre SQLite real: totales por consulta, índice único e inmutabilidad (SC-002, SC-003, SC-006).</summary>
public sealed class CashShiftPersistenceTests : IAsyncLifetime
{
    private TestDb _db = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _db = await TestDb.CreateAsync();

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<Guid> OpenShiftAsync(long floatCents = 50_000)
    {
        await using var context = _db.CreateDbContext();
        var repository = new CashShiftRepository(context);
        var shift = CashShift.Open(await repository.NextNumberAsync(Ct), Money.FromCents(floatCents), _db.User.UserId, _db.Clock.UtcNow);
        repository.Add(shift);
        Assert.Equal(Pos.Application.Products.SaveStatus.Saved, (await repository.SaveChangesAsync(Ct)).Status);
        return shift.Id;
    }

    [Fact]
    public async Task TotalesDelTurno_CoincidenConElCalculoManual()
    {
        await OpenShiftAsync(50_000);
        var product = await SalesTestSupport.SeedProductAsync(_db, "TUR-1", tracks: false, priceCents: 10_000);
        await SalesTestSupport.SellOkAsync(_db, (product, 2000));
        var second = await SalesTestSupport.SellOkAsync(_db, (product, 1000));
        Assert.True((await SalesTestSupport.CancelAsync(_db, second.SaleId)).IsSuccess);

        await using var context = _db.CreateDbContext();
        var shift = await new CashShiftRepository(context).GetOpenAsync(CashRegister.Default, Ct);
        var totals = await new SaleRepository(context).GetShiftTotalsAsync(shift!.Id, Ct);

        Assert.Equal(1, totals.SalesCount);
        Assert.Equal(1, totals.CancelledCount);
        Assert.Equal(20_000, totals.TotalSoldCents);
        Assert.Equal(30_000, totals.CashSalesCents);
        Assert.Equal(10_000, totals.CashCancelledCents);
        Assert.Equal(50_000 + 30_000 - 10_000, shift.ExpectedCash(totals));
    }

    [Fact]
    public async Task DosAperturasSimultaneas_UnaFallaPorElIndiceUnico()
    {
        await using var first = _db.CreateDbContext();
        await using var second = _db.CreateDbContext();
        var repositoryA = new CashShiftRepository(first);
        var repositoryB = new CashShiftRepository(second);
        repositoryA.Add(CashShift.Open(1, Money.FromCents(100), _db.User.UserId, _db.Clock.UtcNow));
        repositoryB.Add(CashShift.Open(2, Money.FromCents(100), _db.User.UserId, _db.Clock.UtcNow));

        var a = await repositoryA.SaveChangesAsync(Ct);
        var b = await repositoryB.SaveChangesAsync(Ct);

        Assert.Equal(Pos.Application.Products.SaveStatus.Saved, a.Status);
        Assert.Equal(Pos.Application.Products.SaveStatus.Duplicate, b.Status);
        Assert.Equal(Pos.Application.CashShifts.CashShiftFields.OpenPerRegister, b.DuplicateField);
    }

    [Fact]
    public async Task TurnoCerrado_NoSePuedeModificarNiUnMovimiento()
    {
        var id = await OpenShiftAsync();
        await using (var context = _db.CreateDbContext())
        {
            var repository = new CashShiftRepository(context);
            var shift = (await repository.GetAsync(id, Ct))!;
            repository.AddMovement(shift.RecordDeposit(Money.FromCents(100), "Cambio"));
            await repository.SaveChangesAsync(Ct);
        }

        await using (var context = _db.CreateDbContext())
        {
            var shift = (await new CashShiftRepository(context).GetAsync(id, Ct))!;
            shift.Close(ShiftSalesTotals.Empty, Money.FromCents(50_100), null, _db.User.UserId, _db.Clock.UtcNow);
            await context.SaveChangesAsync(Ct);
        }

        await using (var context = _db.CreateDbContext())
        {
            var shift = (await context.CashShifts.SingleAsync(s => s.Id == id, Ct));
            context.Entry(shift).Property(s => s.ClosingComment).CurrentValue = "otro";
            await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync(Ct));
        }

        await using (var context = _db.CreateDbContext())
        {
            var movement = await context.CashMovements.SingleAsync(Ct);
            context.CashMovements.Remove(movement);
            await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync(Ct));
        }
    }
}
