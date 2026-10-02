using Microsoft.EntityFrameworkCore;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Purchases;
using Pos.Domain.Inventory;
using Pos.Domain.Purchases;
using Pos.Domain.Users;
using Pos.Infrastructure.Tests.TestSupport;
using static Pos.Infrastructure.Tests.TestSupport.PurchaseTestSupport;

namespace Pos.Infrastructure.Tests.Purchases;

/// <summary>020, FR-017a/b: anulación completa con movimientos inversos y bloqueos sobre SQLite real.</summary>
public sealed class VoidPurchaseTests : IAsyncLifetime
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
    public async Task Anular_RestaLaExistencia_EnlazaElMovimiento_YQuedaAnuladaConMotivo()
    {
        var supplier = await _purchases.Suppliers.CreateOkAsync("Norte");
        var refresco = await _purchases.ProductAsync("REF", "3");
        var registered = await _purchases.RegisterOkAsync(supplier, "F-100", Line(refresco, "5", "1.00"));

        var result = await _purchases.VoidAsync(registered.PurchaseId, "  Factura equivocada ");

        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.Equal(3_000, await _purchases.StockOfAsync(refresco.Id));
        var purchase = await _purchases.LoadAsync(registered.PurchaseId);
        Assert.Equal((PurchaseStatus.Voided, "Factura equivocada", _purchases.Users.Admin.Id), (purchase.Status, purchase.VoidReason, purchase.VoidedBy));
        Assert.Equal(_db.Clock.UtcNow, purchase.VoidedAt);

        await using var check = _db.CreateDbContext();
        var voidMovement = await check.InventoryMovements.AsNoTracking().SingleAsync(m => m.Type == MovementType.PurchaseVoid, Ct);
        Assert.Equal(voidMovement.Id, purchase.Lines.Single().VoidMovementId);
        Assert.Equal(("F-100", 3_000L), (voidMovement.Reference, voidMovement.ResultingStockThousandths));
        var entry = await check.AuditEntries.AsNoTracking().SingleAsync(e => e.Action == AuditActions.PurchaseVoided, Ct);
        Assert.Equal(("Factura equivocada", registered.PurchaseId), (entry.Reason, entry.EntityId));
        Assert.Equal(("Vigente", "Anulada"), (entry.Changes[0].Before, entry.Changes[0].After));

        // El kárdex identifica la compra en la entrada y en su anulación.
        var movements = (await _purchases.MovementsAsync(refresco.Id)).Items.Where(m => m.Type is MovementType.Purchase or MovementType.PurchaseVoid).ToList();
        Assert.Equal(2, movements.Count);
        Assert.All(movements, m => Assert.Equal((registered.PurchaseId, "Norte"), (m.PurchaseId!.Value, m.SupplierName)));
    }

    [Fact]
    public async Task ExistenciaInsuficiente_SeBloqueaConElProducto_YNadaCambia()
    {
        var supplier = await _purchases.Suppliers.CreateOkAsync("Norte");
        var refresco = await _purchases.ProductAsync("REF");
        var registered = await _purchases.RegisterOkAsync(supplier, "F-1", Line(refresco, "5", "1.00"));
        await SalesTestSupport.SellOkAsync(_db, (refresco, 3_000));

        var result = await _purchases.VoidAsync(registered.PurchaseId);

        var blocked = Assert.IsType<PurchaseVoidBlocked>(result.Error);
        var line = Assert.Single(blocked.Lines);
        Assert.Equal((refresco.Id, VoidBlockReason.InsufficientStock, 2_000L, 5_000L), (line.ProductId, line.Reason, line.OnHandThousandths, line.RequiredThousandths));
        Assert.Equal(2_000, await _purchases.StockOfAsync(refresco.Id));
        Assert.Equal(PurchaseStatus.Active, (await _purchases.LoadAsync(registered.PurchaseId)).Status);
    }

    [Fact]
    public async Task ProductoInactivo_SeBloquea()
    {
        var supplier = await _purchases.Suppliers.CreateOkAsync("Norte");
        var refresco = await _purchases.ProductAsync("REF");
        var registered = await _purchases.RegisterOkAsync(supplier, "F-1", Line(refresco, "5", "1.00"));
        await using (var context = _db.CreateDbContext())
        {
            var product = await context.Products.SingleAsync(p => p.Id == refresco.Id, Ct);
            product.Update(product.Name, product.Sku, null, product.Price, product.UnitCode, isActive: false, product.TracksInventory, product.MinimumStock);
            await context.SaveChangesAsync(Ct);
        }

        var result = await _purchases.VoidAsync(registered.PurchaseId);

        Assert.Equal(VoidBlockReason.ProductInactive, Assert.Single(Assert.IsType<PurchaseVoidBlocked>(result.Error).Lines).Reason);
        Assert.Equal(5_000, await _purchases.StockOfAsync(refresco.Id));
    }

    [Fact]
    public async Task SinMotivo_AnulacionDoble_YCajero_SeRechazan()
    {
        var supplier = await _purchases.Suppliers.CreateOkAsync("Norte");
        var refresco = await _purchases.ProductAsync("REF");
        var registered = await _purchases.RegisterOkAsync(supplier, "F-1", Line(refresco, "5", "1.00"));

        var noReason = await _purchases.VoidAsync(registered.PurchaseId, "   ");
        Assert.Contains(Assert.IsType<ValidationFailed>(noReason.Error).Errors, e => e.Field == PurchaseFields.Reason);

        _purchases.Users.As(_purchases.Users.Cashier);
        Assert.Equal(new Forbidden(Permission.VoidPurchases, CanBeAuthorized: false), (await _purchases.VoidAsync(registered.PurchaseId)).Error);

        _purchases.Users.As(_purchases.Users.Admin);
        Assert.True((await _purchases.VoidAsync(registered.PurchaseId)).IsSuccess);
        Assert.Equal(new InvalidState(PurchaseMessages.AlreadyVoided), (await _purchases.VoidAsync(registered.PurchaseId)).Error);
    }
}
