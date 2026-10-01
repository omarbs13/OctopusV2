using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Abstractions;
using Pos.Application.Licensing;
using Pos.Application.Receivables;
using Pos.Application.Returns;
using Pos.Application.Returns.ReturnSaleItems;
using Pos.Application.Sales;
using Pos.Application.Sales.CancelSale;
using Pos.Application.Sales.ConfirmSale;
using Pos.Domain.Licensing;
using Pos.Domain.Products;
using Pos.Domain.Returns;
using Pos.Domain.Sales;
using Pos.Domain.Users;
using Pos.Infrastructure.Audit;
using Pos.Infrastructure.CashShifts;
using Pos.Infrastructure.CreditNotes;
using Pos.Infrastructure.Inventory;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Products;
using Pos.Infrastructure.Receivables;
using Pos.Infrastructure.Returns;
using Pos.Infrastructure.Sales;

namespace Pos.Infrastructure.Tests.TestSupport;

/// <summary>Plazo de devoluciones en memoria para las pruebas.</summary>
public sealed class TestReturnsSettingsStore : IReturnsSettingsStore
{
    public ReturnsSettings Current { get; set; } = new();

    public ReturnsSettings Load() => Current;

    public void Save(ReturnsSettings settings) => Current = settings;
}

/// <summary>
/// Arma devoluciones, cancelaciones y cobros con nota de crédito (013) sobre SQLite real con
/// usuarios, permisos y concesiones reales; un ámbito (contexto) por operación como la composición.
/// </summary>
public sealed class ReturnsTestSupport
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly TestDb _db;

    public ReturnsTestSupport(TestDb db, ShiftTestSupport users)
    {
        _db = db;
        Users = users;
    }

    public ShiftTestSupport Users { get; }

    public TestReturnsSettingsStore Settings { get; } = new();

    /// <summary>Reemplaza el repositorio de devoluciones (por ejemplo, para forzar un folio duplicado).</summary>
    public Func<PosDbContext, IReturnRepository>? ReturnsFactory { get; set; }

    public SaleReturnProcessor Processor(PosDbContext context)
    {
        var access = Users.Access(context);
        return new SaleReturnProcessor(
            access,
            Users.Grants,
            new SaleRepository(context),
            ReturnsFactory?.Invoke(context) ?? new ReturnRepository(context),
            new CreditNoteRepository(context),
            new InventoryRepository(context),
            new ReturnCashGate(new CashShiftRepository(context), new SaleRepository(context), access, _db.User, Users.Guard(context), Users.License),
            Settings,
            new AuditLog(context),
            new WriteTransactions(context),
            _db.Clock,
            _db.User,
            NullLogger<SaleReturnProcessor>.Instance,
            Users.License,
            new CreditSettlementService(new ReceivableRepository(context)));
    }

    public CancelSaleHandler CancelHandler(PosDbContext context) =>
        new(
            Users.Access(context),
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
            Processor(context),
            new CreditSettlementService(new ReceivableRepository(context)));

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
            new AuditLog(context));

    /// <summary>Concesión de <c>ApproveReturns</c> de un administrador para el usuario conectado (007).</summary>
    public Guid Grant() => Users.Grants.Issue(Permission.ApproveReturns, _db.User.UserId, Users.Admin.Id);

    public async Task<Result<ReturnResult>> CancelAsync(
        Guid saleId,
        ReturnCompensation compensation = ReturnCompensation.Refund,
        string reason = "Error de captura",
        bool authorized = true)
    {
        await using var context = _db.CreateDbContext();
        var version = (await context.Sales.AsNoTracking().SingleAsync(s => s.Id == saleId, Ct)).Version;
        return await CancelHandler(context).HandleAsync(
            new CancelSaleCommand(saleId, version, reason, authorized ? Grant() : null, compensation),
            Ct);
    }

    public async Task<Result<ReturnResult>> ReturnAsync(
        Guid saleId,
        IReadOnlyList<ReturnLineRequest> lines,
        ReturnCompensation compensation = ReturnCompensation.Refund,
        string reason = "Producto dañado",
        bool authorized = true)
    {
        await using var context = _db.CreateDbContext();
        var version = (await context.Sales.AsNoTracking().SingleAsync(s => s.Id == saleId, Ct)).Version;
        return await new ReturnSaleItemsHandler(Processor(context), new ReturnSaleItemsValidator()).HandleAsync(
            new ReturnSaleItemsCommand(saleId, version, lines, reason, compensation, authorized ? Grant() : null),
            Ct);
    }

    /// <summary>Id de la línea de la venta en la posición indicada (desde 1).</summary>
    public async Task<Guid> LineIdAsync(Guid saleId, int position)
    {
        await using var context = _db.CreateDbContext();
        return (await context.SaleLines.AsNoTracking().SingleAsync(l => l.SaleId == saleId && l.Position == position, Ct)).Id;
    }

    /// <summary>Vende con los pagos indicados; el efectivo recibido es exactamente el aplicado.</summary>
    public async Task<ConfirmedSale> SellAsync(
        Product product,
        long quantityThousandths,
        params (PaymentMethod Method, long AmountCents, string? Reference)[] nonCash)
    {
        await SalesTestSupport.EnsureShiftAsync(_db);
        var total = SaleMath.LineAmount(Pos.Domain.Common.Quantity.FromThousandths(quantityThousandths), product.Price).Cents;
        var cash = total - nonCash.Sum(p => p.AmountCents);
        var payments = new List<PaymentInput>();
        if (cash > 0)
        {
            payments.Add(new PaymentInput(PaymentMethod.Cash, 0, cash, null));
        }

        payments.AddRange(nonCash.Select(p => new PaymentInput(p.Method, p.AmountCents, null, p.Reference)));

        await using var context = _db.CreateDbContext();
        var result = await ConfirmHandler(context).HandleAsync(
            new ConfirmSaleCommand(
                Guid.CreateVersion7(),
                [new ConfirmLineInput(product.Id, quantityThousandths, product.Price.Cents)],
                payments),
            Ct);
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value;
    }

    /// <summary>Vende varias líneas pagadas en efectivo exacto.</summary>
    public async Task<ConfirmedSale> SellLinesAsync(params (Product Product, long QuantityThousandths)[] lines)
    {
        await SalesTestSupport.EnsureShiftAsync(_db);
        await using var context = _db.CreateDbContext();
        var result = await ConfirmHandler(context).HandleAsync(SalesTestSupport.CashSale(Guid.CreateVersion7(), lines), Ct);
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value;
    }

    public async Task<Result<ConfirmedSale>> TrySellWithNoteAsync(Product product, long quantityThousandths, string folio, long noteCents)
    {
        await SalesTestSupport.EnsureShiftAsync(_db);
        var total = SaleMath.LineAmount(Pos.Domain.Common.Quantity.FromThousandths(quantityThousandths), product.Price).Cents;
        var payments = new List<PaymentInput> { new(PaymentMethod.CreditNote, Math.Min(noteCents, total), null, folio) };
        var cash = total - payments[0].AmountCents;
        if (cash > 0)
        {
            payments.Add(new PaymentInput(PaymentMethod.Cash, 0, cash, null));
        }

        await using var context = _db.CreateDbContext();
        return await ConfirmHandler(context).HandleAsync(
            new ConfirmSaleCommand(
                Guid.CreateVersion7(),
                [new ConfirmLineInput(product.Id, quantityThousandths, product.Price.Cents)],
                payments),
            Ct);
    }

    /// <summary>Estado de licencia con solo los módulos comprados activos (la evaluación ya venció).</summary>
    public LicenseState Modular(params LicensedModule[] purchased)
    {
        var state = new LicenseState(_db.Clock);
        var firstRun = _db.Clock.UtcNow.AddDays(-60);
        state.Set(new LicenseRecord(2, "m", firstRun, firstRun, 30, purchased.ToHashSet()));
        return state;
    }
}
