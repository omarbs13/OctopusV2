using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Abstractions;
using Pos.Application.Discounts;
using Pos.Application.Inventory;
using Pos.Application.Purchases;
using Pos.Application.Purchases.GetPurchase;
using Pos.Application.Purchases.RegisterPurchase;
using Pos.Application.Purchases.VoidPurchase;
using Pos.Application.Reports;
using Pos.Application.Reports.GetPurchaseReport;
using Pos.Domain.Products;
using Pos.Domain.Purchases;
using Pos.Infrastructure.Audit;
using Pos.Infrastructure.Inventory;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Products;
using Pos.Infrastructure.Purchases;
using Pos.Infrastructure.Reports;
using Pos.Infrastructure.Suppliers;

namespace Pos.Infrastructure.Tests.TestSupport;

/// <summary>
/// Arma los casos de uso de compras (020) sobre SQLite real con usuarios y permisos reales; un ámbito
/// (contexto) por operación como la composición.
/// </summary>
public sealed class PurchaseTestSupport
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly TestDb _db;

    private PurchaseTestSupport(TestDb db, ShiftTestSupport users)
    {
        _db = db;
        Users = users;
        Suppliers = new SupplierTestSupport(db, users);
    }

    public ShiftTestSupport Users { get; }

    public SupplierTestSupport Suppliers { get; }

    /// <summary>Día local del reloj de prueba: la fecha de factura por omisión.</summary>
    public DateOnly Today => DiscountDates.LocalToday(_db.Clock);

    /// <summary>Reemplaza el repositorio de compras (por ejemplo, para forzar una falla al guardar).</summary>
    public Func<PosDbContext, IPurchaseRepository>? PurchasesFactory { get; set; }

    public static async Task<PurchaseTestSupport> CreateAsync(TestDb db) => new(db, await ShiftTestSupport.CreateAsync(db));

    public RegisterPurchaseHandler RegisterHandler(PosDbContext context) =>
        new(
            Users.Access(context),
            new SupplierRepository(context),
            new ProductRepository(context),
            new InventoryRepository(context),
            PurchasesFactory?.Invoke(context) ?? new PurchaseRepository(context),
            new AuditLog(context),
            new WriteTransactions(context),
            _db.Clock,
            _db.User,
            new RegisterPurchaseValidator(),
            NullLogger<RegisterPurchaseHandler>.Instance);

    public VoidPurchaseHandler VoidHandler(PosDbContext context) =>
        new(
            Users.Access(context),
            new PurchaseRepository(context),
            new ProductRepository(context),
            new InventoryRepository(context),
            new AuditLog(context),
            new WriteTransactions(context),
            _db.Clock,
            _db.User,
            new VoidPurchaseValidator(),
            NullLogger<VoidPurchaseHandler>.Instance);

    /// <summary>Producto que controla inventario, con existencia inicial si se indica.</summary>
    public async Task<Product> ProductAsync(string sku, string? initialStock = null, string unit = "H87", bool tracks = true)
    {
        var product = await InventoryTestSupport.SeedProductAsync(_db, sku, unit, tracks);
        if (initialStock is not null)
        {
            await SalesTestSupport.StockAsync(_db, product, initialStock);
        }

        return product;
    }

    public async Task<Result<PurchaseRegisteredDto>> RegisterAsync(
        Guid supplierId,
        string invoice,
        IReadOnlyList<PurchaseLineInput> lines,
        string? tax = null,
        DateOnly? date = null)
    {
        await using var context = _db.CreateDbContext();
        return await RegisterHandler(context).HandleAsync(new RegisterPurchaseCommand(supplierId, invoice, date ?? Today, lines, tax), Ct);
    }

    /// <summary>Compra que debe registrarse.</summary>
    public async Task<PurchaseRegisteredDto> RegisterOkAsync(Guid supplierId, string invoice, params PurchaseLineInput[] lines)
    {
        var result = await RegisterAsync(supplierId, invoice, lines);
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value;
    }

    public async Task<Result> VoidAsync(Guid purchaseId, string reason = "Factura equivocada", int? expectedVersion = null)
    {
        var version = expectedVersion ?? (await LoadAsync(purchaseId)).Version;
        await using var context = _db.CreateDbContext();
        return await VoidHandler(context).HandleAsync(new VoidPurchaseCommand(purchaseId, version, reason), Ct);
    }

    public async Task<Result<PurchaseDetailDto>> GetAsync(Guid purchaseId)
    {
        await using var context = _db.CreateDbContext();
        return await new GetPurchaseHandler(Users.Access(context), new PurchaseRepository(context))
            .HandleAsync(new GetPurchaseQuery(purchaseId), Ct);
    }

    public async Task<Result<PurchaseReportPage>> ReportAsync(GetPurchaseReportQuery query)
    {
        await using var context = _db.CreateDbContext();
        return await new GetPurchaseReportHandler(Users.Access(context), new PurchaseReportReader(context)).HandleAsync(query, Ct);
    }

    public async Task<Purchase> LoadAsync(Guid purchaseId)
    {
        await using var context = _db.CreateDbContext();
        return await context.Purchases.AsNoTracking().Include(p => p.Lines).SingleAsync(p => p.Id == purchaseId, Ct);
    }

    public async Task<long> StockOfAsync(Guid productId)
    {
        await using var context = _db.CreateDbContext();
        return (await context.ProductStocks.AsNoTracking().SingleOrDefaultAsync(s => s.ProductId == productId, Ct))?.OnHandThousandths ?? 0;
    }

    public async Task<MovementPage> MovementsAsync(Guid productId)
    {
        await using var context = _db.CreateDbContext();
        return await new InventoryRepository(context).SearchMovementsAsync(new MovementSearch(productId, null, null, null, 1, 100), Ct);
    }

    public static PurchaseLineInput Line(Product product, string quantity, string cost) => new(product.Id, quantity, cost);
}
