using Pos.Domain.CashShifts;

namespace Pos.Domain.Tests.CashShifts;

public class CashShiftMathTests
{
    [Fact]
    public void Esperado_CoincideConElCalculoManualConCambioPagosMixtosCancelacionesIngresosYRetiros()
    {
        // Fondo $500.00. Ventas en efectivo (neto de cambio) $300.00 + $150.00 = $450.00; una cancelada
        // en efectivo de $150.00; ingreso $200.00; retiro $100.00. Tarjeta y transferencia no cuentan.
        var totals = new ShiftSalesTotals(
            SalesCount: 2,
            CancelledCount: 1,
            TotalSoldCents: 50_000,
            CashSalesCents: 45_000,
            CashCancelledCents: 15_000,
            CardCents: 5_000,
            TransferCents: 0);

        var expected = CashShiftMath.ExpectedCash(50_000, totals, depositsCents: 20_000, withdrawalsCents: 10_000);

        Assert.Equal(50_000 + 45_000 - 15_000 + 20_000 - 10_000, expected);
        Assert.Equal(90_000, expected);
    }

    [Fact]
    public void Esperado_RestaLosReintegrosEnEfectivoYNoLaNotaDeCreditoEmitida()
    {
        // Fondo $500.00, ventas en efectivo $450.00, reintegro en efectivo $120.00 y una nota de $80.00
        // emitida: la nota no mueve efectivo. Una cancelada heredada (sin devolución) de $50.00 sigue restando.
        var totals = new ShiftSalesTotals(
            SalesCount: 3,
            CancelledCount: 1,
            TotalSoldCents: 40_000,
            CashSalesCents: 45_000,
            CashCancelledCents: 5_000,
            CardCents: 0,
            TransferCents: 0,
            CashRefundsCents: 12_000,
            NonCashRefundsCents: 3_000,
            CreditNotesIssuedCents: 8_000);

        var expected = CashShiftMath.ExpectedCash(50_000, totals, depositsCents: 0, withdrawalsCents: 0);

        Assert.Equal(50_000 + 45_000 - 5_000 - 12_000, expected);
    }

    [Fact]
    public void Esperado_SumaAbonosEnEfectivoYRestaSusAnulaciones_SinVentasACreditoNiAbonosConTarjeta()
    {
        // 014, SC-007: fondo $500.00 y ventas en efectivo $100.00; una venta a crédito de $700.00 y abonos con
        // tarjeta de $400.00 no mueven efectivo; abonos en efectivo $300.00 y una anulación en efectivo de $50.00.
        var totals = new ShiftSalesTotals(
            SalesCount: 2,
            CancelledCount: 0,
            TotalSoldCents: 80_000,
            CashSalesCents: 10_000,
            CashCancelledCents: 0,
            CardCents: 0,
            TransferCents: 0,
            OnAccountSalesCents: 70_000,
            CustomerPaymentsCashCents: 30_000,
            CustomerPaymentsNonCashCents: 40_000,
            CustomerPaymentVoidsCashCents: 5_000,
            CustomerPaymentVoidsNonCashCents: 10_000);

        var expected = CashShiftMath.ExpectedCash(50_000, totals, depositsCents: 0, withdrawalsCents: 0);

        Assert.Equal(50_000 + 10_000 + 30_000 - 5_000, expected);
    }

    [Fact]
    public void CanRefund_DetectaQueLaDevolucionDejariaElEsperadoEnNegativo()
    {
        Assert.True(CashShiftMath.CanRefund(expectedCents: 10_000, saleCashCents: 10_000));
        Assert.False(CashShiftMath.CanRefund(expectedCents: 10_000, saleCashCents: 10_001));
    }

    [Fact]
    public void Diferencia_DistingueSobranteFaltanteYCuadrado()
    {
        Assert.Equal(DifferenceKind.Balanced, CashDifference.From(1_000, 1_000).Kind);
        Assert.Equal(DifferenceKind.Over, CashDifference.From(1_500, 1_000).Kind);

        var shortage = CashDifference.From(900, 1_000);
        Assert.Equal(DifferenceKind.Shortage, shortage.Kind);
        Assert.Equal(100, shortage.Amount.Cents);
    }
}
