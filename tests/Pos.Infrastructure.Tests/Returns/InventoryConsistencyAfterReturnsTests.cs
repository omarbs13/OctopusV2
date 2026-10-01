using Microsoft.EntityFrameworkCore;
using Pos.Domain.Inventory;
using Pos.Domain.Licensing;
using Pos.Domain.Returns;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Returns;

/// <summary>
/// Prueba obligatoria (Principio VI): tras cancelaciones y devoluciones parciales las existencias
/// coinciden con el cálculo manual y cada movimiento regresa exactamente lo que salió (SC-003).
/// </summary>
public sealed class InventoryConsistencyAfterReturnsTests : IAsyncLifetime
{
    private TestDb _db = null!;
    private ShiftTestSupport _users = null!;
    private ReturnsTestSupport _returns = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDb.CreateAsync();
        _users = await ShiftTestSupport.CreateAsync(_db);
        _returns = new ReturnsTestSupport(_db, _users);
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<(long OnHand, List<InventoryMovement> Movements)> StockOfAsync()
    {
        await using var context = _db.CreateDbContext();
        var onHand = (await context.ProductStocks.AsNoTracking().SingleAsync(Ct)).OnHandThousandths;
        return (onHand, await context.InventoryMovements.AsNoTracking().OrderBy(m => m.Sequence).ToListAsync(Ct));
    }

    [Fact]
    public async Task Cancelacion_RegresaExactamenteLoVendidoConMovimientoSaleCancel()
    {
        var product = await SalesTestSupport.SeedProductAsync(_db, "INV-1");
        await SalesTestSupport.StockAsync(_db, product, "10");
        var sale = await _returns.SellAsync(product, 4000);

        Assert.True((await _returns.CancelAsync(sale.SaleId)).IsSuccess);

        var (onHand, movements) = await StockOfAsync();
        Assert.Equal(10_000, onHand);
        Assert.Equal([MovementType.Initial, MovementType.Sale, MovementType.SaleCancellation], movements.Select(m => m.Type));
        Assert.Equal(4_000, movements[2].QuantityThousandths);
        Assert.Equal(sale.Folio, movements[2].Reference);
    }

    [Fact]
    public async Task Cancelacion_ProductoInactivoOBorrado_RegresaLaCantidadIgualmente()
    {
        var inactive = await SalesTestSupport.SeedProductAsync(_db, "INV-2");
        await SalesTestSupport.StockAsync(_db, inactive, "10");
        var sale = await _returns.SellAsync(inactive, 3000);
        await using (var context = _db.CreateDbContext())
        {
            var stored = await context.Products.SingleAsync(Ct);
            stored.Update(stored.Name, stored.Sku, null, stored.Price, "H87", isActive: false, true, null);
            await context.SaveChangesAsync(Ct);
        }

        Assert.True((await _returns.CancelAsync(sale.SaleId)).IsSuccess);

        Assert.Equal(10_000, (await StockOfAsync()).OnHand);
    }

    [Fact]
    public async Task ProductoSinControlDeInventario_NoGeneraMovimiento()
    {
        var plain = await SalesTestSupport.SeedProductAsync(_db, "INV-3", tracks: false);
        var sale = await _returns.SellAsync(plain, 2000);

        Assert.True((await _returns.CancelAsync(sale.SaleId)).IsSuccess);

        await using var check = _db.CreateDbContext();
        Assert.Empty(check.InventoryMovements);
        Assert.Null((await check.SaleReturnLines.AsNoTracking().SingleAsync(Ct)).ReturnMovementId);
    }

    [Fact]
    public async Task SinLicenciaDeInventario_NoHayMovimientos()
    {
        var product = await SalesTestSupport.SeedProductAsync(_db, "INV-4");
        await SalesTestSupport.StockAsync(_db, product, "10");
        var sale = await _returns.SellAsync(product, 4000);
        _users.License = _returns.Modular(LicensedModule.CashShifts, LicensedModule.Returns);

        Assert.True((await _returns.CancelAsync(sale.SaleId)).IsSuccess);

        var (onHand, movements) = await StockOfAsync();
        Assert.Equal(6_000, onHand);
        Assert.Equal([MovementType.Initial, MovementType.Sale], movements.Select(m => m.Type));
    }

    [Fact]
    public async Task DevolucionesParciales_RegresanLoDevueltoConSaleReturnYLasExistenciasCuadran()
    {
        var product = await SalesTestSupport.SeedProductAsync(_db, "INV-5", unit: "KGM", priceCents: 1_000);
        await SalesTestSupport.StockAsync(_db, product, "10");
        var sale = await _returns.SellAsync(product, 4000);
        var line = await _returns.LineIdAsync(sale.SaleId, 1);

        Assert.True((await _returns.ReturnAsync(sale.SaleId, [new ReturnLineRequest(line, 1500)])).IsSuccess);
        Assert.Equal(7_500, (await StockOfAsync()).OnHand);
        Assert.True((await _returns.ReturnAsync(sale.SaleId, [new ReturnLineRequest(line, 2500)])).IsSuccess);

        var (onHand, movements) = await StockOfAsync();
        Assert.Equal(10_000, onHand);
        Assert.Equal(
            [MovementType.Initial, MovementType.Sale, MovementType.SaleReturn, MovementType.SaleReturn],
            movements.Select(m => m.Type));
        Assert.Equal([1_500L, 2_500L], movements.Skip(2).Select(m => m.QuantityThousandths));
    }
}
