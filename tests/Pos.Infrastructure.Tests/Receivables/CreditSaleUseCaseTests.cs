using Microsoft.EntityFrameworkCore;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Sales.ConfirmSale;
using Pos.Domain.Products;
using Pos.Domain.Receivables;
using Pos.Domain.Sales;
using Pos.Domain.Users;
using Pos.Infrastructure.Sales;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Receivables;

/// <summary>
/// 014, Historia 2: venta a crédito con control de límite sobre SQLite real con usuarios, permisos y
/// concesiones reales (FR-005 a FR-008, SC-007, SC-008).
/// </summary>
public sealed class CreditSaleUseCaseTests : IAsyncLifetime
{
    private TestDb _db = null!;
    private CreditTestSupport _credit = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDb.CreateAsync();
        _credit = new CreditTestSupport(_db, await ShiftTestSupport.CreateAsync(_db));
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private Task<Product> ProductAsync(string sku, long priceCents, bool tracks = false) =>
        SalesTestSupport.SeedProductAsync(_db, sku, tracks: tracks, priceCents: priceCents);

    private void AsCashier() => _credit.Users.As(_credit.Users.Cashier);

    [Fact]
    public async Task TotalIgualAlDisponible_SeRegistraSinConcesionYCreaLaCuentaPendiente()
    {
        var ana = await _credit.CreditCustomerAsync(limitCents: 100_000);
        await _credit.InsertPendingReceivableAsync(ana, 70_000);
        var product = await ProductAsync("C-1", 30_000);
        AsCashier();

        var result = await _credit.SellOnCreditAsync(ana, product);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.Equal(0, result.Value.ChangeCents);
        var receivable = await _credit.ReceivableOfAsync(result.Value.SaleId);
        Assert.Equal((30_000L, 30_000L, ReceivableStatus.Pending), (receivable.OriginalCents, receivable.BalanceCents, receivable.Status));
        Assert.Null(receivable.OverLimitAuthorizedBy);
        Assert.Equal("Ana", receivable.CustomerName);
        Assert.Equal(100_000, await _credit.BalanceAsync(ana));
        await using var check = _db.CreateDbContext();
        Assert.True(await check.AuditEntries.AnyAsync(e => e.Action == AuditActions.CreditSaleRegistered && e.EntityId == result.Value.SaleId, Ct));
        Assert.False(await check.AuditEntries.AnyAsync(e => e.Action == AuditActions.CreditLimitOverride, Ct));
        var payment = await check.SalePayments.AsNoTracking().SingleAsync(p => p.SaleId == result.Value.SaleId, Ct);
        Assert.Equal((PaymentMethod.OnAccount, 30_000L, (long?)null, (long?)null), (payment.Method, payment.AmountCents, payment.ReceivedCents, payment.ChangeCents));
    }

    [Fact]
    public async Task CajeroQueExcede_SinConcesion_EsCreditLimitExceededYNoQuedaNada()
    {
        var ana = await _credit.CreditCustomerAsync(limitCents: 100_000);
        await _credit.InsertPendingReceivableAsync(ana, 70_000);
        var product = await ProductAsync("C-2", 40_000, tracks: true);
        await SalesTestSupport.StockAsync(_db, product, "10");
        AsCashier();

        var result = await _credit.SellOnCreditAsync(ana, product);

        Assert.Equal(new CreditLimitExceeded(10_000), result.Error);
        await using var check = _db.CreateDbContext();
        Assert.Equal(1, await check.Sales.CountAsync(Ct));
        Assert.Equal(1, await check.Receivables.CountAsync(Ct));
        Assert.Equal(10_000, (await check.ProductStocks.AsNoTracking().SingleAsync(s => s.ProductId == product.Id, Ct)).OnHandThousandths);
        Assert.Equal(1, await check.InventoryMovements.CountAsync(m => m.ProductId == product.Id, Ct));
    }

    [Fact]
    public async Task ConConcesionValida_SeRegistraConsumeLaConcesionYAuditaElExcedente()
    {
        var ana = await _credit.CreditCustomerAsync(limitCents: 100_000);
        await _credit.InsertPendingReceivableAsync(ana, 70_000);
        var product = await ProductAsync("C-3", 40_000);
        AsCashier();
        var grant = _credit.OverLimitGrant();

        var result = await _credit.SellOnCreditAsync(ana, product, overLimitGrant: grant);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.Null(_credit.Users.Grants.TryConsume(grant, Permission.ApproveCreditOverLimit, _credit.Users.Cashier.Id));
        var receivable = await _credit.ReceivableOfAsync(result.Value.SaleId);
        Assert.Equal(_credit.Users.Admin.Id, receivable.OverLimitAuthorizedBy);
        await using var check = _db.CreateDbContext();
        var audit = await check.AuditEntries.AsNoTracking().SingleAsync(e => e.Action == AuditActions.CreditLimitOverride, Ct);
        Assert.Equal((_credit.Users.Cashier.Id, (Guid?)_credit.Users.Admin.Id, result.Value.SaleId), (audit.CreatedBy, audit.AuthorizedBy, audit.EntityId));
        Assert.Contains("Ana", audit.Details, StringComparison.Ordinal);
        Assert.Contains(result.Value.Folio, audit.Details, StringComparison.Ordinal);
        Assert.Contains("Saldo previo $700.00", audit.Details, StringComparison.Ordinal);
        Assert.Contains("Límite $1,000.00", audit.Details, StringComparison.Ordinal);
        Assert.Contains("Monto $400.00", audit.Details, StringComparison.Ordinal);

        // La concesión ya se consumió: otra venta que excede vuelve a pedir autorización.
        var again = await _credit.SellOnCreditAsync(ana, product, overLimitGrant: grant);
        Assert.Equal(new CreditLimitExceeded(50_000), again.Error);
    }

    [Fact]
    public async Task ConcesionInvalida_RegistraElRechazoEnLaBitacoraYNoLaVenta()
    {
        var ana = await _credit.CreditCustomerAsync(limitCents: 10_000);
        var product = await ProductAsync("C-4", 20_000);
        AsCashier();

        var result = await _credit.SellOnCreditAsync(ana, product, overLimitGrant: Guid.CreateVersion7());

        Assert.Equal(new CreditLimitExceeded(10_000), result.Error);
        await using var check = _db.CreateDbContext();
        Assert.Empty(check.Sales);
        Assert.Empty(check.Receivables);
        var denied = await check.AuditEntries.AsNoTracking().SingleAsync(e => e.Action == AuditActions.AdminAuthorizationDenied, Ct);
        Assert.Equal(ana, denied.EntityId);
    }

    [Fact]
    public async Task Administrador_PasaSinConcesionYQuedaComoAutorizador()
    {
        var ana = await _credit.CreditCustomerAsync(limitCents: 10_000);
        var product = await ProductAsync("C-5", 25_000);

        var result = await _credit.SellOnCreditAsync(ana, product);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.Equal(_credit.Users.Admin.Id, (await _credit.ReceivableOfAsync(result.Value.SaleId)).OverLimitAuthorizedBy);
        await using var check = _db.CreateDbContext();
        var audit = await check.AuditEntries.AsNoTracking().SingleAsync(e => e.Action == AuditActions.CreditLimitOverride, Ct);
        Assert.Equal((_credit.Users.Admin.Id, (Guid?)_credit.Users.Admin.Id), (audit.CreatedBy, audit.AuthorizedBy));
    }

    [Fact]
    public async Task ClienteSoloEfectivoOInactivo_NoEsElegible()
    {
        var cash = (await _credit.CreateAsync("Solo efectivo")).Value;
        var inactive = await _credit.CreditCustomerAsync("Inactiva");
        Assert.True((await _credit.SetActiveAsync(inactive, active: false)).IsSuccess);
        var product = await ProductAsync("C-6", 1_000);

        Assert.IsType<CustomerNotEligibleForCredit>((await _credit.SellOnCreditAsync(cash, product)).Error);
        Assert.IsType<CustomerNotEligibleForCredit>((await _credit.SellOnCreditAsync(inactive, product)).Error);
        Assert.IsType<ValidationFailed>((await _credit.SellOnCreditAsync(null, product)).Error);
        await using var check = _db.CreateDbContext();
        Assert.Empty(check.Sales);
    }

    [Fact]
    public async Task CreditoMezcladoConOtroPago_SeRechaza()
    {
        var ana = await _credit.CreditCustomerAsync();
        var product = await ProductAsync("C-7", 10_000);

        var result = await _credit.SellOnCreditAsync(
            ana,
            product,
            payments: [new PaymentInput(PaymentMethod.OnAccount, 5_000, null, null), new PaymentInput(PaymentMethod.Cash, 0, 5_000, null)]);

        Assert.IsType<ValidationFailed>(result.Error);
        await using var check = _db.CreateDbContext();
        Assert.Empty(check.Sales);
    }

    [Fact]
    public async Task FallaAntesDeConfirmar_NoDejaVentaCuentaNiInventario()
    {
        var ana = await _credit.CreditCustomerAsync();
        var product = await ProductAsync("C-8", 10_000, tracks: true);
        await SalesTestSupport.StockAsync(_db, product, "5");
        _credit.SalesFactory = context => new FailingSaleRepository(new SaleRepository(context));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _credit.SellOnCreditAsync(ana, product, 2000));

        await using var check = _db.CreateDbContext();
        Assert.Empty(check.Sales);
        Assert.Empty(check.Receivables);
        Assert.Equal(5_000, (await check.ProductStocks.AsNoTracking().SingleAsync(Ct)).OnHandThousandths);
        Assert.False(await check.AuditEntries.AnyAsync(e => e.Action == AuditActions.CreditSaleRegistered, Ct));
    }

    [Fact]
    public async Task VentaACredito_NoCambiaElEsperadoDelTurnoYSeSumaEnVentasACredito()
    {
        var ana = await _credit.CreditCustomerAsync();
        var product = await ProductAsync("C-9", 20_000);
        await SalesTestSupport.SellOkAsync(_db, (product, 1000));
        var before = await _credit.OpenShiftNumbersAsync();

        await _credit.SellOnCreditOkAsync(ana, product, 2000);

        var after = await _credit.OpenShiftNumbersAsync();
        Assert.Equal(before.Expected, after.Expected);
        Assert.Equal(40_000, after.Totals.OnAccountSalesCents);
        Assert.Equal(before.Totals.TotalSoldCents + 40_000, after.Totals.TotalSoldCents);
    }

    [Fact]
    public async Task MismoBorrador_DevuelveLaVentaYaRegistradaSinOtraCuenta()
    {
        var ana = await _credit.CreditCustomerAsync();
        var product = await ProductAsync("C-10", 10_000);
        var draft = Guid.CreateVersion7();

        var first = await _credit.SellOnCreditAsync(ana, product, draftId: draft);
        var second = await _credit.SellOnCreditAsync(ana, product, draftId: draft);

        Assert.True(first.IsSuccess, first.Error?.ToString());
        Assert.Equal(first.Value.SaleId, Assert.IsType<AlreadyRegistered>(second.Error).SaleId);
        await using var check = _db.CreateDbContext();
        Assert.Equal(1, await check.Receivables.CountAsync(Ct));
    }

    [Fact]
    public async Task DetalleYBusqueda_MuestranElCreditoDeLaVenta()
    {
        var ana = await _credit.CreditCustomerAsync();
        var product = await ProductAsync("C-11", 10_000);
        var sale = await _credit.SellOnCreditOkAsync(ana, product);

        await using var context = _db.CreateDbContext();
        var repository = new SaleRepository(context);
        var detail = (await repository.GetDetailAsync(sale.SaleId, Ct))!;
        Assert.Equal(new Pos.Application.Sales.CreditInfo(ana, "Ana", 10_000, ReceivableStatus.Pending), detail.Credit);
        var page = await repository.SearchAsync(new Pos.Application.Sales.SaleSearch(null, null, null, null, 1, 100, CustomerId: ana), Ct);
        Assert.Equal(ReceivableStatus.Pending, Assert.Single(page.Items).CreditStatus);
        var ticket = Pos.Application.Printing.Ticket.TicketBuilder.Build(null, detail, 32);
        Assert.Contains(ticket.Lines, l => l.Text == "Cliente: Ana");
        Assert.Contains(ticket.Lines, l => l.Text.StartsWith("A crédito:", StringComparison.Ordinal));
        Assert.DoesNotContain(ticket.Lines, l => l.Text.StartsWith("CAMBIO", StringComparison.Ordinal));
    }
}
