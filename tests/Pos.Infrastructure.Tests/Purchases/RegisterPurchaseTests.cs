using Microsoft.EntityFrameworkCore;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Purchases;
using Pos.Domain.Inventory;
using Pos.Infrastructure.Tests.TestSupport;
using static Pos.Infrastructure.Tests.TestSupport.PurchaseTestSupport;

namespace Pos.Infrastructure.Tests.Purchases;

/// <summary>
/// 020, Historia 2: reglas que viven en <c>RegisterPurchase</c> sobre SQLite real (existencia exacta, enlaces,
/// producto sin cambios, factura duplicada, errores juntos y bitácora). El cálculo y las reglas del agregado se
/// prueban en Domain.
/// </summary>
public sealed class RegisterPurchaseTests : IAsyncLifetime
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
    public async Task Registrar_SumaExistenciaExacta_ConMovimientosEnlazadosYElProductoSinCambios()
    {
        var supplier = await _purchases.Suppliers.CreateOkAsync("Norte");
        var refresco = await _purchases.ProductAsync("REF", "10");
        var queso = await _purchases.ProductAsync("QUE", unit: "KGM");

        var result = await _purchases.RegisterAsync(supplier, "F-100", [Line(refresco, "5", "12.50"), Line(queso, "2.5", "40.00")], "26.00");

        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.Equal((16_250L, 2_600L, 18_850L), (result.Value.SubtotalCents, result.Value.TaxCents, result.Value.TotalCents));
        Assert.Equal(15_000, await _purchases.StockOfAsync(refresco.Id));
        Assert.Equal(2_500, await _purchases.StockOfAsync(queso.Id));

        var purchase = await _purchases.LoadAsync(result.Value.PurchaseId);
        Assert.Equal(2, purchase.LineCount);
        Assert.Equal(purchase.Lines.Sum(l => l.AmountCents), purchase.SubtotalCents);
        Assert.Equal(purchase.SubtotalCents + purchase.TaxCents, purchase.TotalCents);

        await using var check = _db.CreateDbContext();
        var movements = await check.InventoryMovements.AsNoTracking().Where(m => m.Type == MovementType.Purchase).ToListAsync(Ct);
        Assert.Equal(2, movements.Count);
        Assert.All(movements, m => Assert.Equal("F-100", m.Reference));
        Assert.Equal(
            movements.Select(m => m.Id).Order(),
            purchase.Lines.Select(l => l.MovementId).Order());

        // SC-002: registrar no cambia el precio ni los datos del producto.
        var after = await check.Products.AsNoTracking().SingleAsync(p => p.Id == refresco.Id, Ct);
        Assert.Equal((refresco.Price, refresco.Name, refresco.Version), (after.Price, after.Name, after.Version));
    }

    [Fact]
    public async Task SegundaCompraAOtroCosto_NoCambiaElCostoDeLaPrimera()
    {
        var supplier = await _purchases.Suppliers.CreateOkAsync("Norte");
        var refresco = await _purchases.ProductAsync("REF");
        var first = await _purchases.RegisterOkAsync(supplier, "F-1", Line(refresco, "5", "12.50"));

        await _purchases.RegisterOkAsync(supplier, "F-2", Line(refresco, "5", "13.00"));

        Assert.Equal(1_250, (await _purchases.LoadAsync(first.PurchaseId)).Lines.Single().UnitCostCents);
    }

    [Fact]
    public async Task FacturaDuplicada_DelMismoProveedorSeRechaza_DeOtroSeAcepta_YTrasAnularSeLibera()
    {
        var norte = await _purchases.Suppliers.CreateOkAsync("Norte");
        var sur = await _purchases.Suppliers.CreateOkAsync("Sur");
        var refresco = await _purchases.ProductAsync("REF");
        var first = await _purchases.RegisterOkAsync(norte, "F-100", Line(refresco, "5", "1.00"));

        var duplicate = await _purchases.RegisterAsync(norte, " f-100", [Line(refresco, "1", "1.00")]);

        var error = Assert.IsType<DuplicateInvoice>(duplicate.Error);
        Assert.Equal(first.PurchaseId, error.PurchaseId);
        Assert.Equal(_purchases.Today, error.InvoiceDate);
        Assert.Equal(5_000, await _purchases.StockOfAsync(refresco.Id));

        await _purchases.RegisterOkAsync(sur, "F-100", Line(refresco, "1", "1.00"));
        Assert.True((await _purchases.VoidAsync(first.PurchaseId)).IsSuccess);
        await _purchases.RegisterOkAsync(norte, "f-100", Line(refresco, "1", "1.00"));
    }

    [Fact]
    public async Task ProveedorInactivoYProductoSinInventario_UnSoloErrorConAmbosCampos_YNadaCambia()
    {
        var supplier = await _purchases.Suppliers.CreateOkAsync("Norte");
        Assert.True((await _purchases.Suppliers.SetActiveAsync(supplier, active: false)).IsSuccess);
        var refresco = await _purchases.ProductAsync("REF", "10");
        var servicio = await _purchases.ProductAsync("SRV", tracks: false);

        var result = await _purchases.RegisterAsync(supplier, "F-1", [Line(refresco, "5", "1.00"), Line(servicio, "1", "1.00")]);

        var failed = Assert.IsType<ValidationFailed>(result.Error);
        Assert.Contains(failed.Errors, e => e.Field == PurchaseFields.SupplierId);
        Assert.Contains(failed.Errors, e => e.Field == PurchaseFields.LineProduct(1));
        Assert.Equal(10_000, await _purchases.StockOfAsync(refresco.Id));
        await using var check = _db.CreateDbContext();
        Assert.Equal(0, await check.Purchases.CountAsync(Ct));
        Assert.Equal(0, await check.InventoryMovements.CountAsync(m => m.Type == MovementType.Purchase, Ct));
    }

    [Fact]
    public async Task Registrar_AgregaALaBitacoraProveedorFacturaEImportes()
    {
        var supplier = await _purchases.Suppliers.CreateOkAsync("Norte");
        var refresco = await _purchases.ProductAsync("REF");

        var result = await _purchases.RegisterAsync(supplier, "F-100", [Line(refresco, "5", "12.50")], "2.00");

        await using var check = _db.CreateDbContext();
        var entry = await check.AuditEntries.AsNoTracking().SingleAsync(e => e.Action == AuditActions.PurchaseRegistered, Ct);
        Assert.Equal((AuditActions.PurchaseEntity, result.Value.PurchaseId, "Compra F-100 · Norte"), (entry.EntityType, entry.EntityId, entry.EntityName));
        Assert.Equal(
            ["Proveedor", "Factura", "Fecha de factura", "Líneas", "Subtotal", "Impuestos", "Total"],
            entry.Changes.Select(c => c.Field));
        Assert.Equal(["Norte", "F-100", "$62.50", "$2.00", "$64.50"], entry.Changes.Where(c => c.Field is "Proveedor" or "Factura" or "Subtotal" or "Impuestos" or "Total").Select(c => c.After));
    }
}
