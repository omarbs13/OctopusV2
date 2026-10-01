using Microsoft.EntityFrameworkCore;
using Pos.Application.Abstractions;
using Pos.Domain.Licensing;
using Pos.Domain.Products;
using Pos.Domain.Receivables;
using Pos.Domain.Returns;
using Pos.Domain.Sales;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Receivables;

/// <summary>014, FR-016 y research §8: cancelaciones y devoluciones de ventas a crédito sobre SQLite real.</summary>
public sealed class CreditReturnTests : IAsyncLifetime
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

    private async Task<SaleReturn> LastReturnAsync(Guid saleId)
    {
        await using var context = _db.CreateDbContext();
        return await context.SaleReturns.AsNoTracking().Include(r => r.Refunds)
            .OrderByDescending(r => r.Number).FirstAsync(r => r.SaleId == saleId, Ct);
    }

    [Fact]
    public async Task CancelarVentaACreditoSinAbonos_ReduceElSaldoYCancelaLaCuenta()
    {
        var ana = await _credit.CreditCustomerAsync();
        var sale = await _credit.SellOnCreditOkAsync(ana, await ProductAsync("R-1", 20_000));
        var before = await _credit.OpenShiftNumbersAsync();

        var result = await _credit.Returns.CancelAsync(sale.SaleId);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        var receivable = await _credit.ReceivableOfAsync(sale.SaleId);
        Assert.Equal((ReceivableStatus.Cancelled, 0L), (receivable.Status, receivable.BalanceCents));
        Assert.Equal(0, await _credit.BalanceAsync(ana));
        var refund = Assert.Single((await LastReturnAsync(sale.SaleId)).Refunds);
        Assert.Equal((PaymentMethod.OnAccount, 20_000L, RefundStatus.Settled), (refund.Method, refund.AmountCents, refund.Status));
        Assert.Equal(before.Expected, (await _credit.OpenShiftNumbersAsync()).Expected);
        await _credit.AssertLedgerAsync();
    }

    [Fact]
    public async Task CancelarVentaParcialmenteAbonada_ReaplicaElExcedenteALasOtrasCuentas()
    {
        // Quickstart escenario 9: ventas de 200, 500 y 400; abono de 600; cancelar la de 500.
        var ana = await _credit.CreditCustomerAsync(limitCents: 1_000_000);
        await _credit.SellOnCreditOkAsync(ana, await ProductAsync("R-200", 20_000));
        _db.Clock.Advance(TimeSpan.FromMinutes(1));
        var five = await _credit.SellOnCreditOkAsync(ana, await ProductAsync("R-500", 50_000));
        _db.Clock.Advance(TimeSpan.FromMinutes(1));
        var four = await _credit.SellOnCreditOkAsync(ana, await ProductAsync("R-400", 40_000));
        _db.Clock.Advance(TimeSpan.FromMinutes(1));
        await _credit.PayOkAsync(ana, 60_000);
        _db.Clock.Advance(TimeSpan.FromMinutes(1));
        var before = await _credit.OpenShiftNumbersAsync();

        var result = await _credit.Returns.CancelAsync(five.SaleId);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.Equal(ReceivableStatus.Cancelled, (await _credit.ReceivableOfAsync(five.SaleId)).Status);
        var reapplied = await _credit.ReceivableOfAsync(four.SaleId);
        Assert.Equal((ReceivableStatus.Paid, 0L), (reapplied.Status, reapplied.BalanceCents));
        Assert.Equal(ReceivableEntryType.ExcessIn, Assert.Single(reapplied.Entries).Type);
        Assert.Equal(0, await _credit.BalanceAsync(ana));
        var refund = Assert.Single((await LastReturnAsync(five.SaleId)).Refunds);
        Assert.Equal((PaymentMethod.OnAccount, 50_000L, RefundStatus.Settled), (refund.Method, refund.AmountCents, refund.Status));
        Assert.Equal(before.Expected, (await _credit.OpenShiftNumbersAsync()).Expected);
        await _credit.AssertLedgerAsync();
    }

    [Fact]
    public async Task SinOtrasDeudas_ReintegraElExcedenteEnEfectivoDesdeElTurno()
    {
        var ana = await _credit.CreditCustomerAsync();
        var sale = await _credit.SellOnCreditOkAsync(ana, await ProductAsync("R-3", 50_000));
        _db.Clock.Advance(TimeSpan.FromMinutes(1));
        await _credit.PayOkAsync(ana, 30_000);
        _db.Clock.Advance(TimeSpan.FromMinutes(1));
        var before = await _credit.OpenShiftNumbersAsync();

        var result = await _credit.Returns.CancelAsync(sale.SaleId);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        var refunds = (await LastReturnAsync(sale.SaleId)).Refunds;
        Assert.Equal(2, refunds.Count);
        Assert.Single(refunds.Select(r => r.SalePaymentId).Distinct());
        Assert.Contains(refunds, r => (r.Method, r.AmountCents, r.Status) == (PaymentMethod.OnAccount, 20_000L, RefundStatus.Settled));
        Assert.Contains(refunds, r => (r.Method, r.AmountCents, r.Status) == (PaymentMethod.Cash, 30_000L, RefundStatus.Paid));
        var after = await _credit.OpenShiftNumbersAsync();
        Assert.Equal(before.Expected - 30_000, after.Expected);
        Assert.Equal(30_000, after.Totals.CashRefundsCents);
        await _credit.AssertLedgerAsync();
    }

    [Fact]
    public async Task SinEfectivoSuficienteParaElReintegro_EsInsufficientCashSinCambios()
    {
        var ana = await _credit.CreditCustomerAsync();
        var sale = await _credit.SellOnCreditOkAsync(ana, await ProductAsync("R-4", 50_000));
        await _credit.PayOkAsync(ana, 30_000, PaymentMethod.Card);

        var result = await _credit.Returns.CancelAsync(sale.SaleId);

        Assert.Equal(new InsufficientCash(null), result.Error);
        Assert.Equal(ReceivableStatus.Pending, (await _credit.ReceivableOfAsync(sale.SaleId)).Status);
        await using var check = _db.CreateDbContext();
        Assert.Empty(check.SaleReturns);
        Assert.Equal(SaleStatus.Completed, (await check.Sales.AsNoTracking().SingleAsync(s => s.Id == sale.SaleId, Ct)).Status);
    }

    [Fact]
    public async Task CompensacionConNotaDeCredito_SeRechaza()
    {
        var ana = await _credit.CreditCustomerAsync();
        var sale = await _credit.SellOnCreditOkAsync(ana, await ProductAsync("R-5", 10_000));

        var result = await _credit.Returns.CancelAsync(sale.SaleId, ReturnCompensation.CreditNote);

        Assert.IsType<ValidationFailed>(result.Error);
        await using var check = _db.CreateDbContext();
        Assert.Empty(check.CreditNotes);
        Assert.Empty(check.SaleReturns);
    }

    [Fact]
    public async Task DevolucionParcial_ReduceElSaldoYLaVentaTotalmenteDevueltaCancelaLaCuenta()
    {
        var ana = await _credit.CreditCustomerAsync();
        var sale = await _credit.SellOnCreditOkAsync(ana, await ProductAsync("R-6", 10_000), 3000);
        var line = await _credit.Returns.LineIdAsync(sale.SaleId, 1);

        Assert.True((await _credit.Returns.ReturnAsync(sale.SaleId, [new ReturnLineRequest(line, 1000)])).IsSuccess);
        var partial = await _credit.ReceivableOfAsync(sale.SaleId);
        Assert.Equal((ReceivableStatus.Pending, 20_000L), (partial.Status, partial.BalanceCents));

        Assert.True((await _credit.Returns.ReturnAsync(sale.SaleId, [new ReturnLineRequest(line, 2000)])).IsSuccess);
        Assert.Equal(ReceivableStatus.Cancelled, (await _credit.ReceivableOfAsync(sale.SaleId)).Status);
        await _credit.AssertLedgerAsync();
    }

    [Fact]
    public async Task AnularUnAbonoAplicadoAUnaVentaDevueltaDespues_EsInvalidState()
    {
        var ana = await _credit.CreditCustomerAsync();
        var sale = await _credit.SellOnCreditOkAsync(ana, await ProductAsync("R-7", 50_000));
        _db.Clock.Advance(TimeSpan.FromMinutes(1));
        var payment = await _credit.PayOkAsync(ana, 30_000);
        _db.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True((await _credit.Returns.CancelAsync(sale.SaleId)).IsSuccess);

        var result = await _credit.VoidAsync(payment.PaymentId);

        Assert.IsType<InvalidState>(result.Error);
        Assert.Equal(Pos.Domain.Receivables.CustomerPaymentStatus.Active, (await _credit.PaymentAsync(payment.PaymentId)).Status);
    }

    [Fact]
    public async Task CancelacionBasicaSinLicenciaDeDevoluciones_TambienAjustaElSaldo()
    {
        var ana = await _credit.CreditCustomerAsync();
        var older = await _credit.SellOnCreditOkAsync(ana, await ProductAsync("R-8a", 10_000));
        _db.Clock.Advance(TimeSpan.FromMinutes(1));
        var sale = await _credit.SellOnCreditOkAsync(ana, await ProductAsync("R-8b", 20_000));
        _db.Clock.Advance(TimeSpan.FromMinutes(1));
        await _credit.PayOkAsync(ana, 5_000);
        _db.Clock.Advance(TimeSpan.FromMinutes(1));
        _credit.Users.License = _credit.Returns.Modular(LicensedModule.CreditAndCustomers, LicensedModule.CashShifts);

        var result = await _credit.BasicCancelAsync(sale.SaleId);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.Equal(ReceivableStatus.Cancelled, (await _credit.ReceivableOfAsync(sale.SaleId)).Status);
        Assert.Equal(5_000, (await _credit.ReceivableOfAsync(older.SaleId)).BalanceCents);
        Assert.Equal(5_000, await _credit.BalanceAsync(ana));
        await _credit.AssertLedgerAsync();
    }

    [Fact]
    public async Task CancelacionBasicaQueDevolveriaEfectivo_SeRechazaSinCambios()
    {
        var ana = await _credit.CreditCustomerAsync();
        var sale = await _credit.SellOnCreditOkAsync(ana, await ProductAsync("R-9", 20_000));
        _db.Clock.Advance(TimeSpan.FromMinutes(1));
        await _credit.PayOkAsync(ana, 15_000);
        _credit.Users.License = _credit.Returns.Modular(LicensedModule.CreditAndCustomers, LicensedModule.CashShifts);

        var result = await _credit.BasicCancelAsync(sale.SaleId);

        Assert.IsType<InvalidState>(result.Error);
        Assert.Equal((ReceivableStatus.Pending, 5_000L), ((await _credit.ReceivableOfAsync(sale.SaleId)).Status, (await _credit.ReceivableOfAsync(sale.SaleId)).BalanceCents));
        await using var check = _db.CreateDbContext();
        Assert.Equal(SaleStatus.Completed, (await check.Sales.AsNoTracking().SingleAsync(s => s.Id == sale.SaleId, Ct)).Status);
    }
}
