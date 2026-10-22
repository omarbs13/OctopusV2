using Microsoft.EntityFrameworkCore;
using Pos.Application.Sales;
using Pos.Application.Sales.ConfirmSale;
using Pos.Domain.Inventory;
using Pos.Domain.Products;
using Pos.Domain.Returns;
using Pos.Domain.Sales;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Sales;

/// <summary>
/// Prueba obligatoria (Principio VI, 026): una loseta con inventario en cajas vendida por metro cuadrado descuenta
/// las cajas equivalentes (m² ÷ embalaje) y cancelar o devolver regresa exactamente lo que salió, sin residuos.
/// </summary>
public sealed class TileSaleInventoryTests : IAsyncLifetime
{
    private TestDb _db = null!;
    private ReturnsTestSupport _returns = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDb.CreateAsync();
        _returns = new ReturnsTestSupport(_db, await ShiftTestSupport.CreateAsync(_db));
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<Product> SeedTileAsync(string sku = "TILE-1", long packaging = 1640)
    {
        var product = Product.Create(
            new ProductDetails($"Piedra {sku}", "33 X 33", PackagingThousandths: packaging, PricePerSqmCents: 17503, PricePerBoxCents: 28705),
            sku,
            null,
            "XBX",
            tracksInventory: true);
        await using (var context = _db.CreateDbContext())
        {
            context.Products.Add(product);
            await context.SaveChangesAsync(Ct);
        }

        await SalesTestSupport.StockAsync(_db, product, "10");
        return product;
    }

    private async Task<ConfirmedSale> SellSquareMetersAsync(Product product, long squareMeterThousandths, long expectedTotalCents)
    {
        await SalesTestSupport.EnsureShiftAsync(_db);
        await using var context = _db.CreateDbContext();
        var result = await _returns.ConfirmHandler(context).HandleAsync(
            new ConfirmSaleCommand(
                Guid.CreateVersion7(),
                [new ConfirmLineInput(product.Id, squareMeterThousandths, 17503, Mode: SaleMode.SquareMeter)],
                [new PaymentInput(PaymentMethod.Cash, 0, expectedTotalCents, null)]),
            Ct);
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value;
    }

    private async Task<long> OnHandAsync()
    {
        await using var context = _db.CreateDbContext();
        return (await context.ProductStocks.AsNoTracking().SingleAsync(Ct)).OnHandThousandths;
    }

    [Fact]
    public async Task VenderPorM2_DescuentaLasCajasEquivalentesYGuardaLaUnidadVendida()
    {
        var product = await SeedTileAsync();

        var sale = await SellSquareMetersAsync(product, 4920, 86115);

        Assert.Equal(7_000, await OnHandAsync()); // 4.92 m² ÷ 1.64 = 3.000 cajas
        await using var context = _db.CreateDbContext();
        var line = await context.SaleLines.AsNoTracking().SingleAsync(Ct);
        Assert.Equal("MTK", line.UnitCode);
        Assert.Equal(17503, line.UnitPriceCents);
        Assert.Equal(4920, line.QuantityThousandths);
        Assert.Equal(86115, line.AmountCents);
        Assert.Equal("Piedra TILE-1 33 X 33", line.ProductName);
        Assert.Equal(3000, (await context.InventoryMovements.AsNoTracking().SingleAsync(m => m.Type == MovementType.Sale, Ct)).QuantityThousandths);
        Assert.NotEmpty(sale.Folio);
    }

    [Fact]
    public async Task Cancelar_ReintegraExactamenteLasCajas()
    {
        var product = await SeedTileAsync();
        var sale = await SellSquareMetersAsync(product, 4920, 86115);

        Assert.True((await _returns.CancelAsync(sale.SaleId)).IsSuccess);

        Assert.Equal(10_000, await OnHandAsync());
        await using var context = _db.CreateDbContext();
        Assert.Equal(3000, (await context.InventoryMovements.AsNoTracking().SingleAsync(m => m.Type == MovementType.SaleCancellation, Ct)).QuantityThousandths);
    }

    [Fact]
    public async Task DevolverParcialEnM2_NoDejaResiduosDeRedondeo()
    {
        var product = await SeedTileAsync();
        // 4.93 m² ÷ 1.64 = 3.0060975 → salen 3.006 cajas.
        var sale = await SellSquareMetersAsync(product, 4930, 86290);
        Assert.Equal(6_994, await OnHandAsync());
        var lineId = await _returns.LineIdAsync(sale.SaleId, 1);

        foreach (var part in new long[] { 1000, 1000, 1000, 1930 })
        {
            var result = await _returns.ReturnAsync(sale.SaleId, [new ReturnLineRequest(lineId, part)]);
            Assert.True(result.IsSuccess, result.Error?.ToString());
        }

        Assert.Equal(10_000, await OnHandAsync());
    }

    [Fact]
    public async Task ProductoSinEmbalajeYConInventario_NoSeVendePorM2()
    {
        var product = Product.Create(
            new ProductDetails("Piedra sin embalaje", PricePerSqmCents: 17503, PricePerBoxCents: 28705),
            "TILE-2",
            null,
            "XBX",
            tracksInventory: true);
        await using (var context = _db.CreateDbContext())
        {
            context.Products.Add(product);
            await context.SaveChangesAsync(Ct);
        }

        await SalesTestSupport.StockAsync(_db, product, "10");
        await SalesTestSupport.EnsureShiftAsync(_db);
        await using var sellContext = _db.CreateDbContext();

        var result = await _returns.ConfirmHandler(sellContext).HandleAsync(
            new ConfirmSaleCommand(
                Guid.CreateVersion7(),
                [new ConfirmLineInput(product.Id, 4920, 17503, Mode: SaleMode.SquareMeter)],
                [new PaymentInput(PaymentMethod.Cash, 0, 86115, null)]),
            Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal(10_000, await OnHandAsync());
    }

    [Fact]
    public async Task VentaAnteriorEnPiezaOCaja_SigueRegresandoLoMismo()
    {
        var product = await SeedTileAsync("TILE-3");
        await SalesTestSupport.EnsureShiftAsync(_db);
        await using var context = _db.CreateDbContext();
        var sold = await _returns.ConfirmHandler(context).HandleAsync(
            new ConfirmSaleCommand(
                Guid.CreateVersion7(),
                [new ConfirmLineInput(product.Id, 3000, 28705)],
                [new PaymentInput(PaymentMethod.Cash, 0, 86115, null)]),
            Ct);
        Assert.True(sold.IsSuccess, sold.Error?.ToString());
        Assert.Equal(7_000, await OnHandAsync());

        Assert.True((await _returns.CancelAsync(sold.Value.SaleId)).IsSuccess);

        Assert.Equal(10_000, await OnHandAsync());
    }
}
