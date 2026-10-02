using Pos.Domain.CashShifts;
using Pos.Domain.Common;

namespace Pos.Domain.Tests.CashShifts;

public class ShiftCutTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static readonly ShiftSalesTotals Totals = new(
        SalesCount: 3,
        CancelledCount: 1,
        TotalSoldCents: 90_000,
        CashSalesCents: 60_000,
        CashCancelledCents: 5_000,
        CardCents: 20_000,
        TransferCents: 10_000,
        CashRefundsCents: 3_000,
        CustomerPaymentsCashCents: 4_000,
        CustomerPaymentVoidsCashCents: 1_000);

    private static CashShift OpenWithMovements()
    {
        var shift = CashShift.Open(7, Money.FromCents(50_000), Guid.NewGuid(), Now.AddHours(-4));
        shift.RecordDeposit(Money.FromCents(20_000), "Cambio");
        shift.RecordWithdrawal(Money.FromCents(15_000), "Resguardo", expectedCashCents: 100_000, authorizedBy: null);
        return shift;
    }

    [Fact]
    public void CorteXyCorteZ_DanElMismoEsperadoQueCashShiftMathYElXNoAlteraElTurno()
    {
        var shift = OpenWithMovements();
        var expected = CashShiftMath.ExpectedCash(50_000, Totals, 20_000, 15_000);

        var generatedBy = Guid.NewGuid();
        var readout = ShiftCut.Readout(1, shift, Totals, generatedBy, authorizedBy: null, Now);

        Assert.Equal(generatedBy, readout.GeneratedBy);
        Assert.Equal(expected, readout.ExpectedCashCents);
        Assert.Equal(20_000, readout.DepositsCents);
        Assert.Equal(15_000, readout.WithdrawalsCents);
        Assert.Null(readout.CountedCashCents);
        Assert.Null(readout.DifferenceCents);
        Assert.Equal("X-000001", readout.Folio);
        Assert.Equal("T-000007", readout.ShiftFolio);
        Assert.Equal(CashShiftStatus.Open, shift.Status);
        Assert.Null(shift.ExpectedCashCents);
        Assert.Equal(1, shift.Version);

        shift.Close(Totals, Money.FromCents(expected - 500), "Faltó cambio", shift.OpenedBy, Now);
        var closing = ShiftCut.Closing(1, shift, shift.OpenedBy);

        Assert.Equal(expected, closing.ExpectedCashCents);
        Assert.Equal(expected - 500, closing.CountedCashCents);
        Assert.Equal(-500, closing.DifferenceCents);
        Assert.Equal("Faltó cambio", closing.Comment);
        Assert.Equal(Now, closing.GeneratedAt);
        Assert.Equal("Z-000001", closing.Folio);
    }

    [Fact]
    public void CorteXSobreTurnoCerradoYCorteZSobreTurnoAbiertoSeRechazan()
    {
        var open = OpenWithMovements();
        Assert.Throws<DomainException>(() => ShiftCut.Closing(1, open, open.OpenedBy));

        var closed = OpenWithMovements();
        closed.Close(Totals, Money.FromCents(closed.ExpectedCash(Totals)), comment: null, closed.OpenedBy, Now);
        Assert.Throws<DomainException>(() => ShiftCut.Readout(1, closed, Totals, Guid.NewGuid(), authorizedBy: null, Now));
    }
}
