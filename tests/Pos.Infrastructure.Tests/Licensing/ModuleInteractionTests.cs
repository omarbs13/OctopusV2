using Microsoft.EntityFrameworkCore;
using Pos.Application.Licensing;
using Pos.Domain.Inventory;
using Pos.Domain.Licensing;
using Pos.Domain.Sales;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Licensing;

/// <summary>
/// 012, H4 y FR-021: con Turnos o Inventario sin licencia la venta se completa omitiendo esa parte, sin
/// error, y los datos del módulo bloqueado se conservan (SC-005).
/// </summary>
public sealed class ModuleInteractionTests : IAsyncLifetime
{
    private TestDb _db = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _db = await TestDb.CreateAsync();

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private LicenseState Modular(params LicensedModule[] purchased)
    {
        var state = new LicenseState(_db.Clock);
        var firstRun = _db.Clock.UtcNow.AddDays(-60);
        state.Set(new LicenseRecord(2, "m", firstRun, firstRun, 30, purchased.ToHashSet()));
        return state;
    }

    private async Task<Pos.Application.Abstractions.Result<Pos.Application.Sales.ConfirmedSale>> SellAsync(
        ILicenseState license,
        Pos.Domain.Products.Product product,
        long quantity)
    {
        await using var context = _db.CreateDbContext();
        return await SalesTestSupport.ConfirmHandler(_db, context, license: license)
            .HandleAsync(SalesTestSupport.CashSale(Guid.CreateVersion7(), (product, quantity)), Ct);
    }

    [Fact]
    public async Task ConTurnosSinLicencia_LaVentaSeRegistraSinTurno()
    {
        var product = await SalesTestSupport.SeedProductAsync(_db, "MOD-1");
        await SalesTestSupport.StockAsync(_db, product, "10");

        var result = await SellAsync(Modular(LicensedModule.Inventory), product, 1000);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        await using var check = _db.CreateDbContext();
        Assert.Null((await check.Sales.AsNoTracking().SingleAsync(Ct)).CashShiftId);
    }

    [Fact]
    public async Task ConInventarioSinLicencia_NoValidaExistenciasNiGeneraMovimientos()
    {
        var product = await SalesTestSupport.SeedProductAsync(_db, "MOD-2");
        await SalesTestSupport.StockAsync(_db, product, "2");
        await SalesTestSupport.EnsureShiftAsync(_db);

        // Se vende más de lo que hay: con Inventario inactivo no se valida la existencia.
        var result = await SellAsync(Modular(LicensedModule.CashShifts), product, 5000);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        await using var check = _db.CreateDbContext();
        Assert.Equal(2_000, (await check.ProductStocks.SingleAsync(Ct)).OnHandThousandths);
        Assert.Equal([MovementType.Initial], await check.InventoryMovements.AsNoTracking().Select(m => m.Type).ToListAsync(Ct));
        var line = (await check.SaleLines.AsNoTracking().SingleAsync(Ct));
        Assert.Null(line.SaleMovementId);
    }

    [Fact]
    public async Task CancelarConInventarioSinLicencia_NoRepone_YLosDatosPreviosSeConservan()
    {
        var product = await SalesTestSupport.SeedProductAsync(_db, "MOD-3");
        await SalesTestSupport.StockAsync(_db, product, "10");
        var sale = await SalesTestSupport.SellOkAsync(_db, (product, 4000));

        Guid saleId = sale.SaleId;
        await using (var context = _db.CreateDbContext())
        {
            var version = (await context.Sales.AsNoTracking().SingleAsync(Ct)).Version;
            var result = await SalesTestSupport.CancelHandler(_db, context, license: Modular(LicensedModule.CashShifts))
                .HandleAsync(new Pos.Application.Sales.CancelSale.CancelSaleCommand(saleId, version, "Error"), Ct);
            Assert.True(result.IsSuccess, result.Error?.ToString());
        }

        await using var check = _db.CreateDbContext();
        Assert.Equal(SaleStatus.Cancelled, (await check.Sales.AsNoTracking().SingleAsync(Ct)).Status);

        // Sin reposición: la existencia sigue como la dejó la venta y los movimientos previos están intactos.
        Assert.Equal(6_000, (await check.ProductStocks.SingleAsync(Ct)).OnHandThousandths);
        Assert.Equal(
            [MovementType.Initial, MovementType.Sale],
            await check.InventoryMovements.AsNoTracking().OrderBy(m => m.Sequence).Select(m => m.Type).ToListAsync(Ct));
    }

    [Fact]
    public async Task ConTodosLosModulosActivos_ElComportamientoNoCambia()
    {
        var product = await SalesTestSupport.SeedProductAsync(_db, "MOD-4");
        await SalesTestSupport.StockAsync(_db, product, "10");
        await SalesTestSupport.EnsureShiftAsync(_db);

        var result = await SellAsync(Modular(LicensedModule.Inventory, LicensedModule.CashShifts), product, 3000);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        await using var check = _db.CreateDbContext();
        Assert.Equal(7_000, (await check.ProductStocks.SingleAsync(Ct)).OnHandThousandths);
        Assert.NotNull((await check.Sales.AsNoTracking().SingleAsync(Ct)).CashShiftId);
    }
}
