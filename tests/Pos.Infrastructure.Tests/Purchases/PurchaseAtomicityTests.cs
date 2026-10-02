using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Products;
using Pos.Application.Purchases;
using Pos.Domain.Purchases;
using Pos.Infrastructure.Purchases;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Purchases;

/// <summary>020, FR-012, SC-006: la compra, sus movimientos y la existencia se guardan juntos o nada.</summary>
public sealed class PurchaseAtomicityTests : IAsyncLifetime
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
    public async Task FallaAlGuardar_NoDejaCompraMovimientosNiCambioDeExistencia()
    {
        var supplier = await _purchases.Suppliers.CreateOkAsync("Norte");
        var refresco = await _purchases.ProductAsync("REF", "10");
        _purchases.PurchasesFactory = context => new FailAfterSave(new PurchaseRepository(context));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _purchases.RegisterAsync(supplier, "F-1", [PurchaseTestSupport.Line(refresco, "5", "1.00")]));

        await using var check = _db.CreateDbContext();
        Assert.Equal(0, await check.Purchases.CountAsync(Ct));
        Assert.Equal(0, await check.PurchaseLines.CountAsync(Ct));
        Assert.Equal(1, await check.InventoryMovements.CountAsync(Ct));
        Assert.Equal(10_000, await _purchases.StockOfAsync(refresco.Id));
    }

    [Fact]
    public async Task CompraDe200Lineas_SeGuardaCompleta()
    {
        var supplier = await _purchases.Suppliers.CreateOkAsync("Norte");
        var lines = new List<PurchaseLineInput>();
        for (var i = 0; i < 200; i++)
        {
            lines.Add(PurchaseTestSupport.Line(await _purchases.ProductAsync($"P-{i:000}"), "3", "1.25"));
        }

        var result = await _purchases.RegisterAsync(supplier, "F-200", lines);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        var purchase = await _purchases.LoadAsync(result.Value.PurchaseId);
        Assert.Equal(200, purchase.LineCount);
        Assert.Equal(200 * 375, purchase.SubtotalCents);
        await using var check = _db.CreateDbContext();
        Assert.Equal(200, await check.InventoryMovements.CountAsync(Ct));
        Assert.Equal(200, await check.ProductStocks.CountAsync(s => s.OnHandThousandths == 3_000, Ct));
    }

    /// <summary>Medición (plan.md, Performance Goals): 200 líneas en menos de 1 s. Explícita para no depender del equipo de CI.</summary>
    [Fact(Explicit = true)]
    public async Task CompraDe200Lineas_SeGuardaEnMenosDeUnSegundo()
    {
        var supplier = await _purchases.Suppliers.CreateOkAsync("Norte");
        var lines = new List<PurchaseLineInput>();
        for (var i = 0; i < 200; i++)
        {
            lines.Add(PurchaseTestSupport.Line(await _purchases.ProductAsync($"P-{i:000}"), "3", "1.25"));
        }

        var watch = Stopwatch.StartNew();
        var result = await _purchases.RegisterAsync(supplier, "F-200", lines);
        watch.Stop();

        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1), $"Tardó {watch.Elapsed.TotalMilliseconds:N0} ms");
    }

    private sealed class FailAfterSave(IPurchaseRepository inner) : IPurchaseRepository
    {
        public Task<Purchase?> GetAsync(Guid id, CancellationToken ct) => inner.GetAsync(id, ct);

        public Task<Purchase?> FindActiveByInvoiceAsync(Guid supplierId, string invoiceKey, CancellationToken ct) =>
            inner.FindActiveByInvoiceAsync(supplierId, invoiceKey, ct);

        public void Add(Purchase purchase) => inner.Add(purchase);

        public Task<PurchaseDetailDto?> GetDetailAsync(Guid id, CancellationToken ct) => inner.GetDetailAsync(id, ct);

        public async Task<SaveOutcome> SaveChangesAsync(CancellationToken ct)
        {
            await inner.SaveChangesAsync(ct);
            throw new InvalidOperationException("Falla forzada antes de confirmar.");
        }
    }
}
