using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Abstractions;
using Pos.Application.Inventory.RegisterMovement;
using Pos.Application.Sales;
using Pos.Application.Sales.CancelSale;
using Pos.Application.Sales.ConfirmSale;
using Pos.Application.Sales.SaveSaleDraft;
using Pos.Domain.Common;
using Pos.Domain.Inventory;
using Pos.Domain.Products;
using Pos.Domain.Sales;
using Pos.Infrastructure.Audit;
using Pos.Infrastructure.Inventory;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Products;
using Pos.Infrastructure.Sales;

namespace Pos.Infrastructure.Tests.TestSupport;

/// <summary>Ayudantes para armar ventas sobre SQLite real, como lo hace la composición (un ámbito por operación).</summary>
public static class SalesTestSupport
{
    public static ConfirmSaleHandler ConfirmHandler(TestDb db, PosDbContext context, ISaleRepository? sales = null) =>
        new(new AllowAllAccessControl(), 
            new ProductRepository(context),
            new InventoryRepository(context),
            sales ?? new SaleRepository(context),
            new SqliteSaleDraftStore(context, db.Clock, db.User),
            new WriteTransactions(context),
            new ConfirmSaleValidator(),
            NullLogger<ConfirmSaleHandler>.Instance);

    public static CancelSaleHandler CancelHandler(TestDb db, PosDbContext context, ISaleRepository? sales = null) =>
        new(new AllowAllAccessControl(), 
            sales ?? new SaleRepository(context),
            new InventoryRepository(context),
            new AuditLog(context),
            new WriteTransactions(context),
            db.Clock,
            db.User,
            new CancelSaleValidator(),
            NullLogger<CancelSaleHandler>.Instance);

    public static SaveSaleDraftHandler SaveDraftHandler(TestDb db, PosDbContext context) =>
        new(new AllowAllAccessControl(), new SqliteSaleDraftStore(context, db.Clock, db.User), NullLogger<SaveSaleDraftHandler>.Instance);

    public static async Task<Product> SeedProductAsync(
        IDbContextFactory<PosDbContext> factory,
        string sku,
        string unit = "H87",
        bool tracks = true,
        long priceCents = 1000)
    {
        var product = Product.Create($"Producto {sku}", sku, null, Money.FromCents(priceCents), unit, tracks);
        await using var context = factory.CreateDbContext();
        context.Products.Add(product);
        await context.SaveChangesAsync();
        return product;
    }

    /// <summary>Inventario inicial (o entrada) por el caso de uso real.</summary>
    public static async Task StockAsync(
        TestDb db,
        Product product,
        string quantity,
        MovementType type = MovementType.Initial)
    {
        await using var context = db.CreateDbContext();
        var result = await InventoryTestSupport.Handler(context)
            .HandleAsync(new RegisterMovementCommand(product.Id, type, quantity, null, null), TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
    }

    /// <summary>Comando de venta pagada exactamente en efectivo, con la cantidad y el precio actuales de cada producto.</summary>
    public static ConfirmSaleCommand CashSale(Guid draftId, params (Product Product, long QuantityThousandths)[] lines)
    {
        var total = lines.Sum(l => SaleMath.LineAmount(Quantity.FromThousandths(l.QuantityThousandths), l.Product.Price).Cents);
        return new ConfirmSaleCommand(
            draftId,
            [.. lines.Select(l => new ConfirmLineInput(l.Product.Id, l.QuantityThousandths, l.Product.Price.Cents))],
            [new PaymentInput(PaymentMethod.Cash, 0, total, null)]);
    }

    public static async Task<Result<ConfirmedSale>> SellAsync(TestDb db, ConfirmSaleCommand command)
    {
        await using var context = db.CreateDbContext();
        return await ConfirmHandler(db, context).HandleAsync(command, TestContext.Current.CancellationToken);
    }

    public static async Task<ConfirmedSale> SellOkAsync(TestDb db, params (Product Product, long QuantityThousandths)[] lines)
    {
        var result = await SellAsync(db, CashSale(Guid.CreateVersion7(), lines));
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value;
    }

    public static async Task<Result> CancelAsync(TestDb db, Guid saleId, string reason = "Error de captura", int? expectedVersion = null)
    {
        await using var context = db.CreateDbContext();
        var version = expectedVersion ?? (await context.Sales.AsNoTracking().SingleAsync(s => s.Id == saleId, TestContext.Current.CancellationToken)).Version;
        return await CancelHandler(db, context).HandleAsync(new CancelSaleCommand(saleId, version, reason), TestContext.Current.CancellationToken);
    }
}
