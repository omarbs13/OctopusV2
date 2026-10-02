using Microsoft.EntityFrameworkCore;
using Pos.Application.Abstractions;
using Pos.Domain.Inventory;
using Pos.Infrastructure.Tests.TestSupport;
using static Pos.Infrastructure.Tests.TestSupport.PurchaseTestSupport;

namespace Pos.Infrastructure.Tests.Purchases;

/// <summary>020, research §6: registro y anulación se serializan con las ventas y las demás compras.</summary>
public sealed class PurchaseConcurrencyTests : IAsyncLifetime
{
    private TestDb _db = null!;
    private PurchaseTestSupport _purchases = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDb.CreateAsync();
        _purchases = await PurchaseTestSupport.CreateAsync(_db);
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task DosComprasSimultaneasDelMismoProducto_SumanAmbasCantidades()
    {
        var supplier = await _purchases.Suppliers.CreateOkAsync("Norte");
        var refresco = await _purchases.ProductAsync("REF", "10");

        var results = await Task.WhenAll(
            Task.Run(() => _purchases.RegisterAsync(supplier, "F-1", [Line(refresco, "5", "1.00")]), Ct),
            Task.Run(() => _purchases.RegisterAsync(supplier, "F-2", [Line(refresco, "7", "1.00")]), Ct));

        Assert.All(results, r => Assert.True(r.IsSuccess, r.Error?.ToString()));
        Assert.Equal(22_000, await _purchases.StockOfAsync(refresco.Id));
    }

    [Fact]
    public async Task DosComprasSimultaneasConLaMismaFactura_SoloUnaSeGuarda()
    {
        var supplier = await _purchases.Suppliers.CreateOkAsync("Norte");
        var refresco = await _purchases.ProductAsync("REF");

        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            Task.Run(() => _purchases.RegisterAsync(supplier, "F-100", [Line(refresco, "5", "1.00")]), Ct)));

        Assert.Single(results, r => r.IsSuccess);
        Assert.IsType<DuplicateInvoice>(Assert.Single(results, r => !r.IsSuccess).Error);
        Assert.Equal(5_000, await _purchases.StockOfAsync(refresco.Id));
        await using var check = _db.CreateDbContext();
        Assert.Equal(1, await check.Purchases.CountAsync(Ct));
    }

    /// <summary>
    /// Caso límite de spec.md: la anulación y la venta se serializan. Si la venta va primero, la existencia ya no
    /// alcanza y la anulación se rechaza; si la anulación va primero, nunca deja la existencia bajo cero. En
    /// ambos casos el kárdex queda consistente.
    /// </summary>
    [Fact]
    public async Task AnulacionYVentaSimultaneas_LaAnulacionNuncaDejaLaExistenciaBajoCero()
    {
        var supplier = await _purchases.Suppliers.CreateOkAsync("Norte");
        var refresco = await _purchases.ProductAsync("REF");
        var purchase = await _purchases.RegisterOkAsync(supplier, "F-1", Line(refresco, "5", "1.00"));
        await SalesTestSupport.EnsureShiftAsync(_db);
        var version = (await _purchases.LoadAsync(purchase.PurchaseId)).Version;

        var voidTask = Task.Run(() => _purchases.VoidAsync(purchase.PurchaseId, expectedVersion: version), Ct);
        var saleTask = Task.Run(() => SalesTestSupport.SellAsync(_db, SalesTestSupport.CashSale(Guid.CreateVersion7(), (refresco, 3_000))), Ct);
        var voided = await voidTask;
        var sold = await saleTask;

        Assert.True(sold.IsSuccess, sold.Error?.ToString());
        await using var check = _db.CreateDbContext();
        var movements = await check.InventoryMovements.AsNoTracking()
            .Where(m => m.ProductId == refresco.Id)
            .OrderBy(m => m.Sequence)
            .ToListAsync(Ct);
        if (voided.IsSuccess)
        {
            Assert.True(movements.Single(m => m.Type == MovementType.PurchaseVoid).ResultingStockThousandths >= 0);
        }
        else
        {
            Assert.IsType<PurchaseVoidBlocked>(voided.Error);
            Assert.Equal(2_000, await _purchases.StockOfAsync(refresco.Id));
        }

        Assert.Equal(Enumerable.Range(1, movements.Count), movements.Select(m => m.Sequence));
        Assert.Equal(movements[^1].ResultingStockThousandths, await _purchases.StockOfAsync(refresco.Id));
    }
}
