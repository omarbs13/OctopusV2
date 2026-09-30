using Pos.Domain.Common;
using Pos.Domain.Sales;

namespace Pos.Domain.Tests.Sales;

public class SaleTests
{
    private static Money M(string text) => Money.Parse(text).Value!.Value;

    private static SaleLine Line(int position, string price = "10.00", string quantity = "1") =>
        SaleLine.Create(
            position,
            Guid.NewGuid(),
            "Refresco",
            "REF-1",
            "H87",
            0,
            M(price),
            Quantity.Parse(quantity, 3).Value!.Value,
            null);

    private static SalePayment Cash(string amount) =>
        SalePayment.Create(new PaymentEntry(PaymentMethod.Cash, M(amount), M(amount), Money.Zero, null));

    [Fact]
    public void Register_requires_payments_that_add_up_to_the_total()
    {
        Assert.Throws<DomainException>(() =>
            Sale.Register(1, Guid.NewGuid(), [Line(1, "10.00", "2")], [Cash("19.99")]));

        var sale = Sale.Register(1, Guid.NewGuid(), [Line(1, "10.00", "2")], [Cash("20.00")]);

        Assert.Equal(2000, sale.TotalCents);
        Assert.Equal(SaleStatus.Completed, sale.Status);
        Assert.Equal("V-000001", sale.Folio);
    }

    [Fact]
    public void Register_rejects_empty_sales_and_two_cash_payments()
    {
        Assert.Throws<DomainException>(() => Sale.Register(1, Guid.NewGuid(), [], [Cash("10.00")]));
        Assert.Throws<DomainException>(() =>
            Sale.Register(1, Guid.NewGuid(), [Line(1, "10.00", "2")], [Cash("10.00"), Cash("10.00")]));
    }

    [Fact]
    public void Cancel_leaves_status_reason_date_and_user()
    {
        var sale = Sale.Register(1, Guid.NewGuid(), [Line(1)], [Cash("10.00")]);
        var user = Guid.NewGuid();
        var now = new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);

        sale.Cancel("  error de captura ", now, user);

        Assert.Equal(SaleStatus.Cancelled, sale.Status);
        Assert.Equal("error de captura", sale.CancellationReason);
        Assert.Equal(now, sale.CancelledAt);
        Assert.Equal(user, sale.CancelledBy);
    }

    [Fact]
    public void Cancel_without_reason_or_twice_is_rejected()
    {
        var sale = Sale.Register(1, Guid.NewGuid(), [Line(1)], [Cash("10.00")]);
        var now = DateTime.UtcNow;

        Assert.Throws<DomainException>(() => sale.Cancel("  ", now, Guid.NewGuid()));
        Assert.Throws<DomainException>(() => sale.Cancel(new string('x', 251), now, Guid.NewGuid()));
        Assert.Equal(SaleStatus.Completed, sale.Status);

        sale.Cancel("motivo", now, Guid.NewGuid());
        Assert.Throws<DomainException>(() => sale.Cancel("otra vez", now, Guid.NewGuid()));
    }

    [Theory]
    [InlineData("V-000123", 123)]
    [InlineData("v123", 123)]
    [InlineData("123", 123)]
    [InlineData(" 7 ", 7)]
    public void Folio_parses_the_accepted_formats(string text, long expected)
    {
        Assert.True(Folio.TryParse(text, out var number));
        Assert.Equal(expected, number);
    }

    [Theory]
    [InlineData("")]
    [InlineData("V-")]
    [InlineData("0")]
    [InlineData("abc")]
    [InlineData("V-12a")]
    public void Folio_rejects_invalid_text(string text)
    {
        Assert.False(Folio.TryParse(text, out _));
    }
}
