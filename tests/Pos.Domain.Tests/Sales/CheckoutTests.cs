using Pos.Domain.Common;
using Pos.Domain.Sales;

namespace Pos.Domain.Tests.Sales;

public class CheckoutTests
{
    private static Money M(string text) => Money.Parse(text).Value!.Value;

    [Fact]
    public void Cash_over_the_total_gives_change()
    {
        var checkout = new Checkout(M("85.50"));

        checkout.SetCashReceived(M("100"));

        Assert.Equal(1450, checkout.Change.Cents);
        Assert.Equal(0, checkout.Shortfall.Cents);
        Assert.True(checkout.CanConfirm);
        var payment = Assert.Single(checkout.ToPayments());
        Assert.Equal(PaymentMethod.Cash, payment.Method);
        Assert.Equal(8550, payment.Amount.Cents);
        Assert.Equal(10000, payment.Received!.Value.Cents);
        Assert.Equal(1450, payment.Change!.Value.Cents);
    }

    [Fact]
    public void Mixed_payment_that_covers_the_total_has_no_change()
    {
        var checkout = new Checkout(M("200"));
        checkout.AddNonCash(PaymentMethod.Card, M("150"), " 1234 ");
        checkout.SetCashReceived(M("50"));

        Assert.Equal(0, checkout.Change.Cents);
        Assert.True(checkout.CanConfirm);
        var payments = checkout.ToPayments();
        Assert.Equal(20000, payments.Sum(p => p.Amount.Cents));
        Assert.Equal("1234", payments.Single(p => p.Method == PaymentMethod.Card).Reference);
    }

    [Fact]
    public void Underpaying_leaves_a_shortfall_and_cannot_be_confirmed()
    {
        var checkout = new Checkout(M("200"));
        checkout.AddNonCash(PaymentMethod.Transfer, M("100"), null);
        checkout.SetCashReceived(M("50"));

        Assert.Equal(5000, checkout.Shortfall.Cents);
        Assert.False(checkout.CanConfirm);
        Assert.Throws<DomainException>(() => checkout.ToPayments());
    }

    [Fact]
    public void Non_cash_above_the_pending_amount_is_rejected()
    {
        var checkout = new Checkout(M("200"));
        checkout.AddNonCash(PaymentMethod.Card, M("150"), null);

        Assert.Throws<DomainException>(() => checkout.AddNonCash(PaymentMethod.Card, M("50.01"), null));
        Assert.Throws<DomainException>(() => checkout.AddNonCash(PaymentMethod.Card, Money.Zero, null));
        Assert.Throws<DomainException>(() => checkout.AddNonCash(PaymentMethod.Cash, M("10"), null));
        Assert.Single(checkout.Payments);
    }

    [Fact]
    public void Fully_non_cash_payment_has_no_cash_entry()
    {
        var checkout = new Checkout(M("80"));
        checkout.AddNonCash(PaymentMethod.Card, M("80"), null);

        Assert.True(checkout.CanConfirm);
        var payment = Assert.Single(checkout.ToPayments());
        Assert.Equal(PaymentMethod.Card, payment.Method);
    }

    [Fact]
    public void Quick_amounts_use_the_pending_amount_or_replace_the_received_bill()
    {
        var checkout = new Checkout(M("200"));
        checkout.AddNonCash(PaymentMethod.Card, M("120"), null);

        checkout.QuickAmount();
        Assert.Equal(8000, checkout.Received.Cents);

        checkout.QuickAmount(100);
        Assert.Equal(10000, checkout.Received.Cents);

        checkout.QuickAmount(20);
        Assert.Equal(2000, checkout.Received.Cents);
        Assert.Throws<DomainException>(() => checkout.QuickAmount(30));
    }

    [Fact]
    public void Removing_a_payment_restores_the_pending_amount()
    {
        var checkout = new Checkout(M("200"));
        checkout.AddNonCash(PaymentMethod.Card, M("120"), null);

        checkout.RemovePayment(0);

        Assert.Empty(checkout.Payments);
        Assert.Equal(20000, checkout.Pending.Cents);
    }
}
