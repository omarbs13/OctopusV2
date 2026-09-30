using Pos.Domain.CashShifts;
using Pos.Domain.Common;

namespace Pos.Domain.Tests.CashShifts;

public class CashShiftTests
{
    private static readonly DateTime Now = new(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);

    private static CashShift Open(long floatCents = 50_000) =>
        CashShift.Open(1, Money.FromCents(floatCents), Guid.NewGuid(), Now);

    private static ShiftSalesTotals Totals(long cashSalesCents = 0) =>
        new(1, 0, cashSalesCents, cashSalesCents, 0, 0, 0);

    [Fact]
    public void Retiro_IgualAlEsperadoSeAceptaYUnCentavoMasSeRechaza()
    {
        var shift = Open();

        shift.RecordWithdrawal(Money.FromCents(50_000), "Envío a resguardo", expectedCashCents: 50_000, authorizedBy: null);

        var ex = Assert.Throws<InsufficientCashException>(() =>
            shift.RecordWithdrawal(Money.FromCents(1), "Otro", expectedCashCents: 0, authorizedBy: null));
        Assert.Equal(0, ex.AvailableCents);
    }

    [Fact]
    public void Movimiento_ExigeMontoPositivoYMotivo()
    {
        var shift = Open();

        Assert.Throws<DomainException>(() => shift.RecordDeposit(Money.Zero, "Cambio"));
        Assert.Throws<DomainException>(() => shift.RecordDeposit(Money.FromCents(100), "  "));
        var movement = shift.RecordDeposit(Money.FromCents(100), " Cambio ");
        Assert.Equal(1, movement.Sequence);
        Assert.Equal("Cambio", movement.Reason);
    }

    [Fact]
    public void Cierre_CuadradoNoExigeComentarioYGuardaLaInstantanea()
    {
        var shift = Open();
        shift.RecordDeposit(Money.FromCents(20_000), "Más cambio");

        shift.Close(Totals(cashSalesCents: 30_000), Money.FromCents(100_000), comment: null, Guid.NewGuid(), Now);

        Assert.Equal(CashShiftStatus.Closed, shift.Status);
        Assert.Equal(100_000, shift.ExpectedCashCents);
        Assert.Equal(0, shift.DifferenceCents);
        Assert.Equal(20_000, shift.DepositsCents);
    }

    [Fact]
    public void Cierre_ConDiferenciaExigeComentario()
    {
        var shift = Open();

        Assert.Throws<DomainException>(() =>
            shift.Close(Totals(), Money.FromCents(49_000), comment: " ", Guid.NewGuid(), Now));

        shift.Close(Totals(), Money.FromCents(49_000), "Faltó cambio", Guid.NewGuid(), Now);
        Assert.Equal(-1_000, shift.DifferenceCents);
        Assert.Equal(DifferenceKind.Shortage, shift.Difference!.Value.Kind);
    }

    [Fact]
    public void Cierre_ConteoCeroConEsperadoMayorQueCeroEsFaltanteYExigeComentario()
    {
        var shift = Open();

        Assert.Throws<DomainException>(() =>
            shift.Close(Totals(), Money.Zero, comment: null, Guid.NewGuid(), Now));
    }

    [Fact]
    public void TurnoCerrado_RechazaMovimientosYUnSegundoCierre()
    {
        var shift = Open();
        shift.Close(Totals(), Money.FromCents(50_000), null, Guid.NewGuid(), Now);

        Assert.Throws<DomainException>(() => shift.RecordDeposit(Money.FromCents(100), "Cambio"));
        Assert.Throws<DomainException>(() =>
            shift.Close(Totals(), Money.FromCents(50_000), null, Guid.NewGuid(), Now));
    }
}
