using Microsoft.EntityFrameworkCore;
using Pos.Domain.Purchases;
using Pos.Infrastructure.Tests.TestSupport;
using static Pos.Infrastructure.Tests.TestSupport.PurchaseTestSupport;

namespace Pos.Infrastructure.Tests.Purchases;

/// <summary>020, research §10: compras y líneas no se editan ni se borran; solo se anulan.</summary>
public sealed class PurchaseImmutabilityTests : IAsyncLifetime
{
    private TestDb _db = null!;
    private PurchaseTestSupport _purchases = null!;
    private Guid _purchaseId;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDb.CreateAsync();
        _purchases = await PurchaseTestSupport.CreateAsync(_db);
        var supplier = await _purchases.Suppliers.CreateOkAsync("Norte");
        var refresco = await _purchases.ProductAsync("REF", "3");
        _purchaseId = (await _purchases.RegisterOkAsync(supplier, "F-1", Line(refresco, "5", "1.00"))).PurchaseId;
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    [Theory]
    [InlineData(nameof(PurchaseLine.QuantityThousandths), 1L)]
    [InlineData(nameof(PurchaseLine.UnitCostCents), 1L)]
    [InlineData(nameof(PurchaseLine.AmountCents), 1L)]
    public async Task ModificarUnaLinea_Lanza(string property, long value)
    {
        await using var context = _db.CreateDbContext();
        var line = await context.PurchaseLines.SingleAsync(Ct);
        context.Entry(line).Property(property).CurrentValue = value;

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task BorrarUnaLinea_Lanza()
    {
        await using var context = _db.CreateDbContext();
        context.PurchaseLines.Remove(await context.PurchaseLines.SingleAsync(Ct));

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task EnlazarElMovimientoDeAnulacion_SePermiteUnaVez()
    {
        var movementId = await InitialMovementIdAsync();
        await using (var context = _db.CreateDbContext())
        {
            var line = await context.PurchaseLines.SingleAsync(Ct);
            context.Entry(line).Property(nameof(PurchaseLine.VoidMovementId)).CurrentValue = movementId;
            await context.SaveChangesAsync(Ct);
        }

        await using var again = _db.CreateDbContext();
        var linked = await again.PurchaseLines.SingleAsync(Ct);
        again.Entry(linked).Property(nameof(PurchaseLine.VoidMovementId)).CurrentValue = linked.MovementId;
        await Assert.ThrowsAsync<InvalidOperationException>(() => again.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task BorrarUnaCompra_Lanza()
    {
        await using var context = _db.CreateDbContext();
        context.Purchases.Remove(await context.Purchases.SingleAsync(Ct));

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync(Ct));
    }

    [Theory]
    [InlineData(nameof(Purchase.InvoiceNumber), "F-2")]
    [InlineData(nameof(Purchase.TotalCents), 1L)]
    [InlineData(nameof(Purchase.SubtotalCents), 1L)]
    public async Task CambiarFacturaOImportesDeUnaVigente_Lanza(string property, object value)
    {
        await using var context = _db.CreateDbContext();
        var purchase = await context.Purchases.SingleAsync(Ct);
        context.Entry(purchase).Property(property).CurrentValue = value;

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task ModificarUnaAnulada_Lanza()
    {
        Assert.True((await _purchases.VoidAsync(_purchaseId)).IsSuccess);

        await using var context = _db.CreateDbContext();
        var purchase = await context.Purchases.SingleAsync(Ct);
        context.Entry(purchase).Property(nameof(Purchase.VoidReason)).CurrentValue = "Otro motivo";

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync(Ct));
    }

    private async Task<Guid> InitialMovementIdAsync()
    {
        await using var context = _db.CreateDbContext();
        return await context.InventoryMovements.Where(m => m.Sequence == 1).Select(m => m.Id).SingleAsync(Ct);
    }
}
