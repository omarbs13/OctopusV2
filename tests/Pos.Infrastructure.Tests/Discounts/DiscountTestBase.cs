using Microsoft.EntityFrameworkCore;
using Pos.Application.Audit;
using Pos.Application.Sales.ConfirmSale;
using Pos.Domain.Discounts;
using Pos.Domain.Products;
using Pos.Domain.Sales;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Discounts;

/// <summary>Base de las pruebas de descuentos (015): SQLite real, un administrador y un cajero conectado.</summary>
public abstract class DiscountTestBase : IAsyncLifetime
{
    protected TestDb Db { get; private set; } = null!;

    protected DiscountTestSupport Discounts { get; private set; } = null!;

    protected ShiftTestSupport Users => Discounts.Users;

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        Db = await TestDb.CreateAsync();
        Discounts = new DiscountTestSupport(Db, await ShiftTestSupport.CreateAsync(Db));
        Users.As(Users.Cashier);
    }

    public ValueTask DisposeAsync()
    {
        Db.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    protected Task<Product> ProductAsync(string sku, long priceCents) =>
        SalesTestSupport.SeedProductAsync(Db, sku, tracks: false, priceCents: priceCents);

    protected static ConfirmLineInput Line(Product product, long thousandths, LineDiscountInput? discount = null) =>
        new(product.Id, thousandths, product.Price.Cents, discount);

    protected static LineDiscountInput Percent(long basisPoints, Guid? approvalId = null) => new(DiscountMode.Percent, basisPoints, approvalId);

    protected static LineDiscountInput Amount(long cents, Guid? approvalId = null) => new(DiscountMode.Amount, cents, approvalId);

    protected async Task<Sale> LoadSaleAsync(Guid saleId)
    {
        await using var context = Db.CreateDbContext();
        return await context.Sales.AsNoTracking()
            .Include(s => s.Lines)
            .Include(s => s.Payments)
            .Include(s => s.Discounts)
            .SingleAsync(s => s.Id == saleId, Ct);
    }

    protected async Task<int> AuditCountAsync(string action)
    {
        await using var context = Db.CreateDbContext();
        return await context.AuditEntries.CountAsync(e => e.Action == action, Ct);
    }

    protected async Task<int> ApprovalCountAsync()
    {
        await using var context = Db.CreateDbContext();
        return await context.DiscountApprovals.CountAsync(Ct);
    }

    /// <summary>Ventas con descuento autorizado: entradas <c>SALE_DISCOUNTS_APPLIED</c> con autorizador (018, research §7).</summary>
    protected async Task<int> AppliedAuthorizedCountAsync()
    {
        await using var context = Db.CreateDbContext();
        return await context.AuditEntries.CountAsync(e => e.Action == AuditActions.SaleDiscountsApplied && e.AuthorizedBy != null, Ct);
    }
}
