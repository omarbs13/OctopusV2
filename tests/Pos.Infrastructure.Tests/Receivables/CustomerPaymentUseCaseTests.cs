using Microsoft.EntityFrameworkCore;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Domain.CashShifts;
using Pos.Domain.Licensing;
using Pos.Domain.Products;
using Pos.Domain.Receivables;
using Pos.Domain.Sales;
using Pos.Domain.Users;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Receivables;

/// <summary>
/// 014, Historia 3: abonos FIFO, idempotencia, concurrencia, turno, efectivo y anulación sobre SQLite
/// real con usuarios, permisos y concesiones reales (FR-009 a FR-014, SC-004, SC-005, SC-007).
/// </summary>
public sealed class CustomerPaymentUseCaseTests : IAsyncLifetime
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

    private Task<Product> ProductAsync(string sku, long priceCents) =>
        SalesTestSupport.SeedProductAsync(_db, sku, tracks: false, priceCents: priceCents);

    /// <summary>Ana con dos ventas a crédito en el turno abierto: 200 (la más antigua) y 500.</summary>
    private async Task<(Guid Ana, Guid Older, Guid Newer)> AnaWithTwoSalesAsync()
    {
        var ana = await _credit.CreditCustomerAsync(limitCents: 1_000_000);
        var older = await _credit.SellOnCreditOkAsync(ana, await ProductAsync("P-200", 20_000));
        _db.Clock.Advance(TimeSpan.FromMinutes(1));
        var newer = await _credit.SellOnCreditOkAsync(ana, await ProductAsync("P-500", 50_000));
        _db.Clock.Advance(TimeSpan.FromMinutes(1));
        return (ana, older.SaleId, newer.SaleId);
    }

    [Fact]
    public async Task Abono_SeRepartePrimeroALaVentaMasAntiguaYLaSaldadaPasaAPagada()
    {
        var (ana, older, newer) = await AnaWithTwoSalesAsync();

        var receipt = await _credit.PayOkAsync(ana, 30_000);

        Assert.Equal(("AB-000001", 70_000L, 40_000L), (receipt.Folio, receipt.BalanceBeforeCents, receipt.BalanceAfterCents));
        var first = await _credit.ReceivableOfAsync(older);
        var second = await _credit.ReceivableOfAsync(newer);
        Assert.Equal((ReceivableStatus.Paid, 0L), (first.Status, first.BalanceCents));
        Assert.Equal((ReceivableStatus.Pending, 40_000L), (second.Status, second.BalanceCents));
        Assert.Equal([first.Id], receipt.PaidReceivables);
        Assert.Equal(40_000, await _credit.BalanceAsync(ana));
        await using var check = _db.CreateDbContext();
        Assert.True(await check.AuditEntries.AnyAsync(e => e.Action == AuditActions.CustomerPaymentRegistered && e.EntityId == receipt.PaymentId, Ct));
        await _credit.AssertLedgerAsync();
    }

    [Theory]
    [InlineData(70_001)]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task MontoMayorQueElSaldoOCeroOMenor_EsPaymentExceedsBalance(long amount)
    {
        var (ana, _, _) = await AnaWithTwoSalesAsync();

        var result = await _credit.PayAsync(ana, amount);

        Assert.Equal(new PaymentExceedsBalance(70_000), result.Error);
        await using var check = _db.CreateDbContext();
        Assert.Empty(check.CustomerPayments);
    }

    [Theory]
    [InlineData(PaymentMethod.Cash)]
    [InlineData(PaymentMethod.Card)]
    public async Task SinTurnoAbierto_EsShiftRequiredConCualquierFormaDePago(PaymentMethod method)
    {
        var ana = await _credit.CreditCustomerAsync();
        await _credit.InsertPendingReceivableAsync(ana, 10_000);

        Assert.IsType<ShiftRequired>((await _credit.PayAsync(ana, 5_000, method)).Error);
    }

    [Fact]
    public async Task MismoRequestId_DevuelveElAbonoExistenteSinDuplicar()
    {
        var (ana, _, _) = await AnaWithTwoSalesAsync();
        var request = Guid.CreateVersion7();

        var first = await _credit.PayAsync(ana, 30_000, requestId: request);
        var second = await _credit.PayAsync(ana, 30_000, requestId: request);

        Assert.True(first.IsSuccess && second.IsSuccess);
        Assert.Equal(first.Value.PaymentId, second.Value.PaymentId);
        Assert.Equal(first.Value.Folio, second.Value.Folio);
        await using var check = _db.CreateDbContext();
        Assert.Equal(1, await check.CustomerPayments.CountAsync(Ct));
        Assert.Equal(40_000, await _credit.BalanceAsync(ana));
    }

    [Fact]
    public async Task AbonosConcurrentes_NoDejanElSaldoNegativo()
    {
        var (ana, _, _) = await AnaWithTwoSalesAsync();

        var results = await Task.WhenAll(_credit.PayAsync(ana, 50_000), _credit.PayAsync(ana, 50_000));

        Assert.Single(results, r => r.IsSuccess);
        Assert.Equal(new PaymentExceedsBalance(20_000), Assert.Single(results, r => !r.IsSuccess).Error);
        Assert.Equal(20_000, await _credit.BalanceAsync(ana));
        await _credit.AssertLedgerAsync();
    }

    [Fact]
    public async Task AbonoEnEfectivo_SumaAlEsperado_YConTarjetaNo()
    {
        var (ana, _, _) = await AnaWithTwoSalesAsync();
        var before = await _credit.OpenShiftNumbersAsync();

        await _credit.PayOkAsync(ana, 30_000);
        var afterCash = await _credit.OpenShiftNumbersAsync();
        await _credit.PayOkAsync(ana, 10_000, PaymentMethod.Card);
        var afterCard = await _credit.OpenShiftNumbersAsync();

        Assert.Equal(before.Expected + 30_000, afterCash.Expected);
        Assert.Equal(afterCash.Expected, afterCard.Expected);
        Assert.Equal((30_000L, 10_000L), (afterCard.Totals.CustomerPaymentsCashCents, afterCard.Totals.CustomerPaymentsNonCashCents));
    }

    [Fact]
    public async Task Anular_SinConcesion_EsForbiddenAutorizableTambienParaElAdministrador()
    {
        var (ana, _, _) = await AnaWithTwoSalesAsync();
        var payment = await _credit.PayOkAsync(ana, 30_000);

        var result = await _credit.VoidAsync(payment.PaymentId, authorized: false);

        Assert.Equal(new Forbidden(Permission.VoidCustomerPayments, CanBeAuthorized: true), result.Error);
        Assert.Equal(CustomerPaymentStatus.Active, (await _credit.PaymentAsync(payment.PaymentId)).Status);
    }

    [Fact]
    public async Task AnulacionValida_RevierteSaldoYEstadosYRestaDelEsperado_YNoSeAnulaDosVeces()
    {
        var (ana, older, _) = await AnaWithTwoSalesAsync();
        var payment = await _credit.PayOkAsync(ana, 30_000);
        var paid = await _credit.OpenShiftNumbersAsync();

        var result = await _credit.VoidAsync(payment.PaymentId, "Se capturó dos veces");

        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.Equal(70_000, await _credit.BalanceAsync(ana));
        Assert.Equal(ReceivableStatus.Pending, (await _credit.ReceivableOfAsync(older)).Status);
        var voided = await _credit.PaymentAsync(payment.PaymentId);
        Assert.Equal((CustomerPaymentStatus.Voided, "Se capturó dos veces", (Guid?)_credit.Users.Admin.Id), (voided.Status, voided.VoidReason, voided.VoidAuthorizedBy));
        var after = await _credit.OpenShiftNumbersAsync();
        Assert.Equal(paid.Expected - 30_000, after.Expected);
        Assert.Equal(30_000, after.Totals.CustomerPaymentVoidsCashCents);
        await using (var check = _db.CreateDbContext())
        {
            var audit = await check.AuditEntries.AsNoTracking().SingleAsync(e => e.Action == AuditActions.CustomerPaymentVoided, Ct);
            Assert.Equal(_credit.Users.Admin.Id, audit.AuthorizedBy);
            Assert.Contains("Se capturó dos veces", audit.Details, StringComparison.Ordinal);
            Assert.Contains("AB-000001", audit.Details, StringComparison.Ordinal);
        }

        Assert.IsType<InvalidState>((await _credit.VoidAsync(payment.PaymentId)).Error);
        await _credit.AssertLedgerAsync();
    }

    [Fact]
    public async Task Anular_EnEfectivoSinEfectivoSuficiente_EsInsufficientCashSinMonto()
    {
        var (ana, _, _) = await AnaWithTwoSalesAsync();
        var payment = await _credit.PayOkAsync(ana, 30_000);
        await using (var context = _db.CreateDbContext())
        {
            var shift = (await new Pos.Infrastructure.CashShifts.CashShiftRepository(context).GetOpenAsync(CashRegister.Default, Ct))!;
            Assert.True((await _credit.Users.MoveAsync(shift.Id, CashMovementType.Out, 25_000)).IsSuccess);
        }

        var result = await _credit.VoidAsync(payment.PaymentId);

        Assert.Equal(new InsufficientCash(null), result.Error);
        Assert.Equal(CustomerPaymentStatus.Active, (await _credit.PaymentAsync(payment.PaymentId)).Status);
    }

    [Fact]
    public async Task Anular_SinTurnoAbierto_EsShiftRequired()
    {
        var (ana, _, _) = await AnaWithTwoSalesAsync();
        var payment = await _credit.PayOkAsync(ana, 30_000, PaymentMethod.Card);
        await _credit.CloseOpenShiftAsync();

        Assert.IsType<ShiftRequired>((await _credit.VoidAsync(payment.PaymentId)).Error);
    }

    [Fact]
    public async Task SinLicenciaDeTurnos_ElAbonoSeRegistraYSeAnulaSinTurno()
    {
        var ana = await _credit.CreditCustomerAsync();
        await _credit.InsertPendingReceivableAsync(ana, 10_000);
        _credit.Users.License = _credit.Returns.Modular(LicensedModule.CreditAndCustomers);

        var payment = await _credit.PayOkAsync(ana, 4_000);
        var result = await _credit.VoidAsync(payment.PaymentId);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        var saved = await _credit.PaymentAsync(payment.PaymentId);
        Assert.Null(saved.CashShiftId);
        Assert.Null(saved.VoidCashShiftId);
        Assert.Equal(10_000, await _credit.BalanceAsync(ana));
    }

    [Fact]
    public async Task SinLicenciaDeCredito_RegistrarYAnularSonModuleNotLicensed()
    {
        var (ana, _, _) = await AnaWithTwoSalesAsync();
        var payment = await _credit.PayOkAsync(ana, 10_000);
        _credit.Users.License = _credit.Returns.Modular(LicensedModule.CashShifts, LicensedModule.Returns);

        Assert.Equal(new ModuleNotLicensed(LicensedModule.CreditAndCustomers), (await _credit.PayAsync(ana, 1_000)).Error);
        Assert.Equal(new ModuleNotLicensed(LicensedModule.CreditAndCustomers), (await _credit.VoidAsync(payment.PaymentId)).Error);
    }

    [Fact]
    public async Task SecuenciaMixta_ElSaldoDeCadaCuentaEsElOriginalMasSuLibro()
    {
        var (ana, _, _) = await AnaWithTwoSalesAsync();
        var third = await _credit.SellOnCreditOkAsync(ana, await ProductAsync("P-300", 30_000));
        _db.Clock.Advance(TimeSpan.FromMinutes(1));

        var first = await _credit.PayOkAsync(ana, 25_000);
        _db.Clock.Advance(TimeSpan.FromMinutes(1));
        await _credit.PayOkAsync(ana, 15_000, PaymentMethod.Transfer);
        _db.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True((await _credit.VoidAsync(first.PaymentId)).IsSuccess);
        _db.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True((await _credit.Returns.CancelAsync(third.SaleId)).IsSuccess);
        _db.Clock.Advance(TimeSpan.FromMinutes(1));
        await _credit.PayOkAsync(ana, 20_000);

        await _credit.AssertLedgerAsync();
        Assert.Equal(20_000 + 50_000 - 15_000 - 20_000, await _credit.BalanceAsync(ana));
        Assert.Equal(ReceivableStatus.Cancelled, (await _credit.ReceivableOfAsync(third.SaleId)).Status);
    }
}
