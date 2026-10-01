using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Abstractions;
using Pos.Application.Customers.CreateCustomer;
using Pos.Application.Customers.SetCustomerActive;
using Pos.Application.Customers.UpdateCustomer;
using Pos.Application.Receivables;
using Pos.Application.Receivables.RegisterCustomerPayment;
using Pos.Application.Receivables.VoidCustomerPayment;
using Pos.Application.Reports;
using Pos.Application.Sales;
using Pos.Application.Sales.ConfirmSale;
using Pos.Domain.CashShifts;
using Pos.Domain.Common;
using Pos.Domain.Customers;
using Pos.Domain.Receivables;
using Pos.Domain.Products;
using Pos.Domain.Sales;
using Pos.Domain.Users;
using Pos.Infrastructure.Audit;
using Pos.Infrastructure.CashShifts;
using Pos.Infrastructure.CreditNotes;
using Pos.Infrastructure.Customers;
using Pos.Infrastructure.Inventory;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Products;
using Pos.Infrastructure.Receivables;
using Pos.Infrastructure.Sales;

namespace Pos.Infrastructure.Tests.TestSupport;


/// <summary>Plazo de pago en memoria para las pruebas.</summary>
public sealed class TestReceivablesSettingsStore : IReceivablesSettingsStore
{
    public ReceivablesSettings Current { get; set; } = new();

    public ReceivablesSettings Load() => Current;

    public void Save(ReceivablesSettings settings) => Current = settings;
}

/// <summary>
/// Arma los casos de uso de clientes y crédito (014) sobre SQLite real con usuarios, permisos y
/// concesiones reales; un ámbito (contexto) por operación como la composición.
/// </summary>
public sealed class CreditTestSupport
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly TestDb _db;
    private int _productSequence;

    public CreditTestSupport(TestDb db, ShiftTestSupport users)
    {
        _db = db;
        Users = users;
        Returns = new ReturnsTestSupport(db, users);
    }

    public ShiftTestSupport Users { get; }

    public ReturnsTestSupport Returns { get; }

    public TestReceivablesSettingsStore Settings { get; } = new();

    /// <summary>Zona fija (UTC) para que los días vencido no dependan del equipo.</summary>
    public ReportPeriodResolver Periods { get; } = new(TimeZoneInfo.Utc);

    public CreditAging Aging() => new(_db.Clock, Settings, Periods);

    /// <summary>Reemplaza el repositorio de ventas del cobro (por ejemplo, para forzar una falla).</summary>
    public Func<PosDbContext, ISaleRepository>? SalesFactory { get; set; }

    public ConfirmSaleHandler ConfirmHandler(PosDbContext context) =>
        new(
            Users.Access(context),
            new ProductRepository(context),
            new InventoryRepository(context),
            SalesFactory?.Invoke(context) ?? new SaleRepository(context),
            new SqliteSaleDraftStore(context, _db.Clock, _db.User),
            Users.Guard(context),
            new WriteTransactions(context),
            new ConfirmSaleValidator(),
            NullLogger<ConfirmSaleHandler>.Instance,
            Users.License,
            new CreditNoteRepository(context),
            new AuditLog(context),
            new CustomerRepository(context),
            new ReceivableRepository(context),
            _db.User);

    /// <summary>Concesión de <c>ApproveCreditOverLimit</c> de un administrador para el usuario conectado (007).</summary>
    public Guid OverLimitGrant() => Users.Grants.Issue(Permission.ApproveCreditOverLimit, _db.User.UserId, Users.Admin.Id);

    /// <summary>Cobra a crédito <paramref name="quantityThousandths"/> del producto con un único pago <c>ACCOUNT</c> por el total.</summary>
    public async Task<Result<ConfirmedSale>> SellOnCreditAsync(
        Guid? customerId,
        Product product,
        long quantityThousandths = 1000,
        Guid? overLimitGrant = null,
        Guid? draftId = null,
        IReadOnlyList<PaymentInput>? payments = null)
    {
        await SalesTestSupport.EnsureShiftAsync(_db);
        var total = SaleMath.LineAmount(Quantity.FromThousandths(quantityThousandths), product.Price).Cents;
        await using var context = _db.CreateDbContext();
        return await ConfirmHandler(context).HandleAsync(
            new ConfirmSaleCommand(
                draftId ?? Guid.CreateVersion7(),
                [new ConfirmLineInput(product.Id, quantityThousandths, product.Price.Cents)],
                payments ?? [new PaymentInput(PaymentMethod.OnAccount, total, null, null)],
                customerId,
                overLimitGrant),
            Ct);
    }

    /// <summary>Venta a crédito que debe registrarse.</summary>
    public async Task<ConfirmedSale> SellOnCreditOkAsync(Guid customerId, Product product, long quantityThousandths = 1000)
    {
        var result = await SellOnCreditAsync(customerId, product, quantityThousandths);
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value;
    }

    public async Task<Receivable> ReceivableOfAsync(Guid saleId)
    {
        await using var context = _db.CreateDbContext();
        return await context.Receivables.AsNoTracking().Include(r => r.Entries).SingleAsync(r => r.SaleId == saleId, Ct);
    }

    public PaymentShiftGate ShiftGate(PosDbContext context) =>
        new(new CashShiftRepository(context), new SaleRepository(context), Users.Access(context), _db.User, Users.Guard(context), Users.License);

    public async Task<Result<PaymentReceipt>> PayAsync(
        Guid customerId,
        long amountCents,
        PaymentMethod method = PaymentMethod.Cash,
        Guid? requestId = null,
        string? reference = null)
    {
        await using var context = _db.CreateDbContext();
        return await new RegisterCustomerPaymentHandler(
            Users.Access(context),
            new CustomerRepository(context),
            new ReceivableRepository(context),
            new CustomerPaymentRepository(context),
            ShiftGate(context),
            new AuditLog(context),
            new WriteTransactions(context),
            _db.User,
            new RegisterCustomerPaymentValidator(),
            NullLogger<RegisterCustomerPaymentHandler>.Instance)
            .HandleAsync(new RegisterCustomerPaymentCommand(requestId ?? Guid.CreateVersion7(), customerId, amountCents, method, reference), Ct);
    }

    public async Task<PaymentReceipt> PayOkAsync(Guid customerId, long amountCents, PaymentMethod method = PaymentMethod.Cash)
    {
        var result = await PayAsync(customerId, amountCents, method);
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value;
    }

    /// <summary>Concesión de <c>VoidCustomerPayments</c> de un administrador para el usuario conectado (007).</summary>
    public Guid VoidGrant() => Users.Grants.Issue(Permission.VoidCustomerPayments, _db.User.UserId, Users.Admin.Id);

    public async Task<Result> VoidAsync(Guid paymentId, string reason = "Error de captura", bool authorized = true, Guid? grant = null)
    {
        await using var context = _db.CreateDbContext();
        return await new VoidCustomerPaymentHandler(
            Users.Access(context),
            Users.Grants,
            new CustomerRepository(context),
            new ReceivableRepository(context),
            new CustomerPaymentRepository(context),
            ShiftGate(context),
            new AuditLog(context),
            new WriteTransactions(context),
            _db.Clock,
            _db.User,
            new VoidCustomerPaymentValidator(),
            NullLogger<VoidCustomerPaymentHandler>.Instance)
            .HandleAsync(new VoidCustomerPaymentCommand(paymentId, reason, grant ?? (authorized ? VoidGrant() : null)), Ct);
    }

    public async Task<CustomerPayment> PaymentAsync(Guid paymentId)
    {
        await using var context = _db.CreateDbContext();
        return await context.CustomerPayments.AsNoTracking().SingleAsync(p => p.Id == paymentId, Ct);
    }

    /// <summary>Cancelación básica (sin el módulo Devoluciones) con la liquidación de crédito.</summary>
    public async Task<Result<Pos.Application.Returns.ReturnResult>> BasicCancelAsync(Guid saleId, string reason = "Error de captura")
    {
        await using var context = _db.CreateDbContext();
        var version = (await context.Sales.AsNoTracking().SingleAsync(s => s.Id == saleId, Ct)).Version;
        return await new Pos.Application.Sales.CancelSale.CancelSaleHandler(
            Users.Access(context),
            new SaleRepository(context),
            new CashShiftRepository(context),
            new InventoryRepository(context),
            new AuditLog(context),
            new WriteTransactions(context),
            _db.Clock,
            _db.User,
            new Pos.Application.Sales.CancelSale.CancelSaleValidator(),
            NullLogger<Pos.Application.Sales.CancelSale.CancelSaleHandler>.Instance,
            Users.License,
            processor: null,
            new CreditSettlementService(new ReceivableRepository(context)))
            .HandleAsync(new Pos.Application.Sales.CancelSale.CancelSaleCommand(saleId, version, reason), Ct);
    }

    /// <summary>Cierra el turno abierto cuadrado (sin pasar por el caso de uso).</summary>
    public async Task CloseOpenShiftAsync()
    {
        await using var context = _db.CreateDbContext();
        var shift = await new CashShiftRepository(context).GetOpenAsync(CashRegister.Default, Ct);
        if (shift is null)
        {
            return;
        }

        var totals = await new SaleRepository(context).GetShiftTotalsAsync(shift.Id, Ct);
        shift.Close(totals, Money.FromCents(shift.ExpectedCash(totals)), null, _db.User.UserId, _db.Clock.UtcNow);
        await context.SaveChangesAsync(Ct);
    }

    /// <summary>Invariante SC-004: cada saldo es el original más la suma de su libro, entre 0 y el original.</summary>
    public async Task AssertLedgerAsync()
    {
        await using var context = _db.CreateDbContext();
        foreach (var receivable in await context.Receivables.AsNoTracking().Include(r => r.Entries).ToListAsync(Ct))
        {
            Assert.Equal(receivable.OriginalCents + receivable.Entries.Sum(e => e.AmountCents), receivable.BalanceCents);
            Assert.InRange(receivable.BalanceCents, 0, receivable.OriginalCents);
        }
    }

    /// <summary>Esperado y totales del turno abierto.</summary>
    public async Task<(long Expected, ShiftSalesTotals Totals)> OpenShiftNumbersAsync()
    {
        await using var context = _db.CreateDbContext();
        var shift = (await new CashShiftRepository(context).GetOpenAsync(CashRegister.Default, Ct))!;
        var totals = await new SaleRepository(context).GetShiftTotalsAsync(shift.Id, Ct);
        return (shift.ExpectedCash(totals), totals);
    }

    public async Task<Result<Guid>> CreateAsync(
        string name = "Ana",
        string phone = "555-0100",
        string? email = null,
        string? taxId = null,
        long? limitCents = null,
        CreditMode? mode = null)
    {
        await using var context = _db.CreateDbContext();
        return await new CreateCustomerHandler(
            Users.Access(context),
            new CustomerRepository(context),
            new AuditLog(context),
            new WriteTransactions(context),
            new CreateCustomerValidator(),
            NullLogger<CreateCustomerHandler>.Instance)
            .HandleAsync(new CreateCustomerCommand(name, phone, email, taxId, limitCents, mode), Ct);
    }

    /// <summary>Crea un cliente con crédito como Administrador y deja conectado al usuario anterior.</summary>
    public async Task<Guid> CreditCustomerAsync(string name = "Ana", long limitCents = 100_000, string? taxId = null)
    {
        var current = _db.User.UserId;
        Users.As(Users.Admin);
        var result = await CreateAsync(name, limitCents: limitCents, mode: CreditMode.Credit, taxId: taxId);
        Users.As(current == Users.Cashier.Id ? Users.Cashier : Users.Admin);
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value;
    }

    public async Task<Result> UpdateAsync(
        Guid id,
        string name = "Ana",
        string phone = "555-0100",
        string? taxId = null,
        long? limitCents = null,
        CreditMode? mode = null,
        int? expectedVersion = null)
    {
        await using var context = _db.CreateDbContext();
        var version = expectedVersion ?? (await LoadAsync(id)).Version;
        return await new UpdateCustomerHandler(
            Users.Access(context),
            new CustomerRepository(context),
            new AuditLog(context),
            new WriteTransactions(context),
            new UpdateCustomerValidator(),
            NullLogger<UpdateCustomerHandler>.Instance)
            .HandleAsync(new UpdateCustomerCommand(id, version, name, phone, null, taxId, limitCents, mode), Ct);
    }

    public async Task<Result> SetActiveAsync(Guid id, bool active)
    {
        await using var context = _db.CreateDbContext();
        var version = (await LoadAsync(id)).Version;
        return await new SetCustomerActiveHandler(
            Users.Access(context),
            new CustomerRepository(context),
            new AuditLog(context),
            new WriteTransactions(context),
            NullLogger<SetCustomerActiveHandler>.Instance)
            .HandleAsync(new SetCustomerActiveCommand(id, version, active), Ct);
    }

    public async Task<Customer> LoadAsync(Guid id)
    {
        await using var context = _db.CreateDbContext();
        return await context.Customers.AsNoTracking().SingleAsync(c => c.Id == id, Ct);
    }

    public async Task<long> BalanceAsync(Guid customerId)
    {
        await using var context = _db.CreateDbContext();
        return await new CustomerRepository(context).GetBalanceAsync(customerId, Ct);
    }

    /// <summary>
    /// Inserta directamente una venta a crédito y su cuenta <c>PENDING</c> (sin turno ni inventario),
    /// con la fecha del reloj de pruebas. Sirve para preparar saldos sin pasar por el cobro.
    /// </summary>
    public async Task<Guid> InsertPendingReceivableAsync(Guid customerId, long cents)
    {
        var product = await SalesTestSupport.SeedProductAsync(_db, $"CR-{Interlocked.Increment(ref _productSequence):000}", tracks: false, priceCents: cents);
        await using var context = _db.CreateDbContext();
        var customer = await context.Customers.AsNoTracking().SingleAsync(c => c.Id == customerId, Ct);
        var folio = (await context.Sales.MaxAsync(s => (long?)s.FolioNumber, Ct) ?? 0) + 1;
        var line = SaleLine.Create(1, product.Id, product.Name, product.Sku, product.UnitCode, 0, product.Price, Quantity.FromThousandths(1000), null);
        var sale = Sale.Register(
            folio,
            Guid.CreateVersion7(),
            null,
            [line],
            [SalePayment.Create(new PaymentEntry(PaymentMethod.OnAccount, Money.FromCents(cents), null, null, null))]);
        context.Sales.Add(sale);
        context.Receivables.Add(Receivable.Create(sale.Id, customerId, customer.Name, cents, null));
        await context.SaveChangesAsync(Ct);
        return sale.Id;
    }
}
