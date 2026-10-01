using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Abstractions;
using Pos.Application.Discounts;
using Pos.Application.Discounts.ApproveDiscount;
using Pos.Application.Discounts.Coupons.SaveCoupon;
using Pos.Application.Receivables;
using Pos.Application.Returns;
using Pos.Application.Sales;
using Pos.Application.Sales.CancelSale;
using Pos.Application.Sales.ConfirmSale;
using Pos.Domain.Discounts;
using Pos.Domain.Returns;
using Pos.Domain.Users;
using Pos.Infrastructure.Audit;
using Pos.Infrastructure.CashShifts;
using Pos.Infrastructure.CreditNotes;
using Pos.Infrastructure.Discounts;
using Pos.Infrastructure.Inventory;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Products;
using Pos.Infrastructure.Receivables;
using Pos.Infrastructure.Returns;
using Pos.Infrastructure.Sales;
using Pos.Infrastructure.Users;

namespace Pos.Infrastructure.Tests.TestSupport;

/// <summary>Límite de descuento en memoria para las pruebas.</summary>
public sealed class TestDiscountSettingsStore : IDiscountSettingsStore
{
    public DiscountSettings Current { get; set; } = new();

    public DiscountSettings Load() => Current;

    public void Save(DiscountSettings settings) => Current = settings;
}

/// <summary>
/// Arma los casos de uso de descuentos (015) sobre SQLite real con usuarios, permisos y concesiones
/// reales; un ámbito (contexto) por operación como la composición.
/// </summary>
public sealed class DiscountTestSupport
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly TestDb _db;

    public DiscountTestSupport(TestDb db, ShiftTestSupport users)
    {
        _db = db;
        Users = users;
        Returns = new ReturnsTestSupport(db, users);
    }

    public ShiftTestSupport Users { get; }

    public ReturnsTestSupport Returns { get; }

    public TestDiscountSettingsStore Settings { get; } = new();

    public DateOnly Today => DiscountDates.LocalToday(_db.Clock);

    public ConfirmSaleHandler ConfirmHandler(PosDbContext context) =>
        new(
            Users.Access(context),
            new ProductRepository(context),
            new InventoryRepository(context),
            new SaleRepository(context),
            new SqliteSaleDraftStore(context, _db.Clock, _db.User),
            Users.Guard(context),
            new WriteTransactions(context),
            new ConfirmSaleValidator(),
            NullLogger<ConfirmSaleHandler>.Instance,
            Users.License,
            new CreditNoteRepository(context),
            new AuditLog(context),
            currentUser: _db.User,
            discountSettings: Settings,
            approvals: new DiscountApprovalStore(context),
            coupons: new CouponRepository(context),
            clock: _db.Clock);

    public ApproveDiscountHandler ApproveHandler(PosDbContext context) =>
        new(
            Users.Access(context),
            Settings,
            new DiscountApprovalStore(context),
            new UserRepository(context),
            new AuditLog(context),
            new WriteTransactions(context),
            _db.User,
            _db.Clock,
            new ApproveDiscountValidator(),
            NullLogger<ApproveDiscountHandler>.Instance);

    public SaveCouponHandler SaveCouponHandler(PosDbContext context) =>
        new(
            Users.Access(context),
            new CouponRepository(context),
            new ProductRepository(context),
            new AuditLog(context),
            new WriteTransactions(context),
            new SaveCouponValidator(),
            NullLogger<SaveCouponHandler>.Instance);

    /// <summary>Cancelación completa (013) con devolución del uso del cupón.</summary>
    public CancelSaleHandler CancelHandler(PosDbContext context)
    {
        var access = Users.Access(context);
        var release = new CouponUseRelease(new CouponRepository(context), new AuditLog(context), NullLogger<CouponUseRelease>.Instance, Users.License);
        var processor = new SaleReturnProcessor(
            access,
            Users.Grants,
            new SaleRepository(context),
            new ReturnRepository(context),
            new CreditNoteRepository(context),
            new InventoryRepository(context),
            new ReturnCashGate(new CashShiftRepository(context), new SaleRepository(context), access, _db.User, Users.Guard(context), Users.License),
            Returns.Settings,
            new AuditLog(context),
            new WriteTransactions(context),
            _db.Clock,
            _db.User,
            NullLogger<SaleReturnProcessor>.Instance,
            Users.License,
            new CreditSettlementService(new ReceivableRepository(context)),
            release);
        return new CancelSaleHandler(
            access,
            new SaleRepository(context),
            new CashShiftRepository(context),
            new InventoryRepository(context),
            new AuditLog(context),
            new WriteTransactions(context),
            _db.Clock,
            _db.User,
            new CancelSaleValidator(),
            NullLogger<CancelSaleHandler>.Instance,
            Users.License,
            processor,
            new CreditSettlementService(new ReceivableRepository(context)),
            release);
    }

    /// <summary>Concesión de <c>ApproveDiscounts</c> de un administrador para el usuario conectado (007).</summary>
    public Guid Grant() => Users.Grants.Issue(Permission.ApproveDiscounts, _db.User.UserId, Users.Admin.Id);

    public async Task<Result<ConfirmedSale>> SellAsync(ConfirmSaleCommand command)
    {
        await SalesTestSupport.EnsureShiftAsync(_db);
        await using var context = _db.CreateDbContext();
        return await ConfirmHandler(context).HandleAsync(command, Ct);
    }

    public async Task<Result<DiscountApprovalDto>> ApproveAsync(ApproveDiscountCommand command)
    {
        await using var context = _db.CreateDbContext();
        return await ApproveHandler(context).HandleAsync(command, Ct);
    }

    public async Task<Result<Guid>> SaveCouponAsync(SaveCouponCommand command)
    {
        await using var context = _db.CreateDbContext();
        return await SaveCouponHandler(context).HandleAsync(command, Ct);
    }

    public async Task<Coupon> CouponAsync(string code)
    {
        await using var context = _db.CreateDbContext();
        return await context.Coupons.AsNoTracking().SingleAsync(c => c.Code == code, Ct);
    }

    public async Task<Result<ReturnResult>> CancelAsync(Guid saleId)
    {
        await using var context = _db.CreateDbContext();
        var version = (await context.Sales.AsNoTracking().SingleAsync(s => s.Id == saleId, Ct)).Version;
        return await CancelHandler(context).HandleAsync(
            new CancelSaleCommand(saleId, version, "Error de captura", Returns.Grant(), ReturnCompensation.Refund),
            Ct);
    }

    /// <summary>Pago en efectivo exacto por <paramref name="totalCents"/>; ninguno si el total es 0.</summary>
    public static IReadOnlyList<PaymentInput> Cash(long totalCents) =>
        totalCents == 0 ? [] : [new PaymentInput(Pos.Domain.Sales.PaymentMethod.Cash, 0, totalCents, null)];
}
