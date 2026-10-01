using Pos.Domain.Common;
using Pos.Domain.Returns;
using Pos.Domain.Sales;

namespace Pos.Domain.Tests.Returns;

public sealed class SaleReturnTests
{
    private static Money M(string text) => Money.Parse(text).Value!.Value;

    private static SaleLine Line(int position, string price, string quantity) =>
        SaleLine.Create(position, Guid.NewGuid(), "Refresco", "REF-1", "H87", 0, M(price), Quantity.Parse(quantity, 3).Value!.Value, null);

    private static SalePayment Cash(string amount) =>
        SalePayment.Create(new PaymentEntry(PaymentMethod.Cash, M(amount), M(amount), Money.Zero, null));

    private static SaleReturn NewReturn(
        string reason = "Producto dañado",
        long lineCents = 1_000,
        long refundCents = 1_000,
        ReturnCompensation compensation = ReturnCompensation.Refund,
        Guid? creditNoteId = null)
    {
        var line = SaleReturnLine.Create(Guid.NewGuid(), 1_000, lineCents, null);
        var refunds = compensation == ReturnCompensation.Refund
            ? new[] { SaleReturnRefund.Create(Guid.NewGuid(), PaymentMethod.Cash, refundCents) }
            : [];
        return SaleReturn.Create(Guid.NewGuid(), 1, Guid.NewGuid(), ReturnKind.Partial, reason, Guid.NewGuid(), compensation, null, [line], refunds, creditNoteId);
    }

    [Fact]
    public void Create_ExigeMotivoYSumasQueCuadren()
    {
        Assert.Equal("D-000001", NewReturn().Folio);
        Assert.Throws<DomainException>(() => NewReturn(reason: "  "));
        Assert.Throws<DomainException>(() => NewReturn(reason: new string('x', 251)));
        Assert.Throws<DomainException>(() => NewReturn(refundCents: 999));
        Assert.Throws<DomainException>(() => NewReturn(lineCents: 0, refundCents: 1));
        Assert.Throws<DomainException>(() => SaleReturn.Create(
            Guid.NewGuid(), 1, Guid.NewGuid(), ReturnKind.Partial, "x", Guid.NewGuid(), ReturnCompensation.Refund, null, [], [], null));
    }

    [Fact]
    public void Create_ConNotaDeCreditoNoLlevaReintegrosYSiLaNota()
    {
        var ok = NewReturn(compensation: ReturnCompensation.CreditNote, creditNoteId: Guid.NewGuid());

        Assert.Empty(ok.Refunds);
        Assert.Throws<DomainException>(() => NewReturn(compensation: ReturnCompensation.CreditNote));
    }

    [Fact]
    public void MarkReversed_SoloUnaVezYSoloEnPendientes()
    {
        var card = SaleReturnRefund.Create(Guid.NewGuid(), PaymentMethod.Card, 500);
        var cash = SaleReturnRefund.Create(Guid.NewGuid(), PaymentMethod.Cash, 500);

        Assert.Equal(RefundStatus.PendingReversal, card.Status);
        card.MarkReversed(Guid.NewGuid(), DateTime.UtcNow);

        Assert.Equal(RefundStatus.Reversed, card.Status);
        Assert.Throws<InvalidOperationException>(() => card.MarkReversed(Guid.NewGuid(), DateTime.UtcNow));
        Assert.Throws<InvalidOperationException>(() => cash.MarkReversed(Guid.NewGuid(), DateTime.UtcNow));
    }

    [Fact]
    public void ApplyReturn_NoExcedeLoVendidoAcumulaYMarcaTotalmenteDevuelta()
    {
        var line = Line(1, "10.00", "3");
        var sale = Sale.Register(1, Guid.NewGuid(), Guid.NewGuid(), [line], [Cash("30.00")]);

        var first = sale.ApplyReturn([new ReturnLineRequest(line.Id, 1_000)]);
        Assert.Equal(1_000, first[0].AmountCents);
        Assert.True(sale.IsPartiallyReturned);
        Assert.Equal(2_000, line.AvailableToReturn);

        Assert.Throws<DomainException>(() => sale.ApplyReturn([new ReturnLineRequest(line.Id, 2_001)]));

        sale.ApplyReturn([new ReturnLineRequest(line.Id, 2_000)]);
        Assert.True(sale.IsFullyReturned);
        Assert.Equal(sale.TotalCents, sale.ReturnedCents);
        Assert.Throws<DomainException>(() => sale.ApplyReturn([new ReturnLineRequest(line.Id, 1)]));
    }

    [Fact]
    public void ApplyReturn_RechazaSinLineasYVentasCanceladas_YNoPermiteCancelarConDevolucionesPrevias()
    {
        var line = Line(1, "10.00", "2");
        var sale = Sale.Register(1, Guid.NewGuid(), Guid.NewGuid(), [line], [Cash("20.00")]);

        Assert.Throws<DomainException>(() => sale.ApplyReturn([]));
        sale.ApplyReturn([new ReturnLineRequest(line.Id, 1_000)]);
        Assert.Throws<DomainException>(sale.EnsureCanCancelInFull);

        var other = Line(1, "10.00", "1");
        var cancelled = Sale.Register(2, Guid.NewGuid(), Guid.NewGuid(), [other], [Cash("10.00")]);
        cancelled.Cancel("Error", DateTime.UtcNow, Guid.NewGuid());
        Assert.Throws<DomainException>(() => cancelled.ApplyReturn([new ReturnLineRequest(other.Id, 1_000)]));
    }
}
