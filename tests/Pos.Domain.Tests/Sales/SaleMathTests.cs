using Pos.Domain.Common;
using Pos.Domain.Sales;

namespace Pos.Domain.Tests.Sales;

public class SaleMathTests
{
    private static Quantity Q(string text) => Quantity.Parse(text, 3).Value!.Value;

    private static Money M(string text) => Money.Parse(text).Value!.Value;

    [Fact]
    public void Line_amount_rounds_half_up_once()
    {
        // 0.333 × 10.05 = 3.34665 → 3.35
        Assert.Equal(335, SaleMath.LineAmount(Q("0.333"), M("10.05")).Cents);
    }

    [Fact]
    public void Line_amount_rounds_the_midpoint_up()
    {
        // 0.5 × 0.01 = 0.005 → 0.01
        Assert.Equal(1, SaleMath.LineAmount(Q("0.5"), M("0.01")).Cents);
        // 0.499 × 0.01 = 0.00499 → 0.00
        Assert.Equal(0, SaleMath.LineAmount(Q("0.499"), M("0.01")).Cents);
    }

    [Fact]
    public void Line_amount_above_the_maximum_throws()
    {
        Assert.Throws<DomainException>(() => SaleMath.LineAmount(Q("1000"), M("999999.99")));
        Assert.Throws<DomainException>(() => SaleMath.LineAmount(Q("9999999"), M("999999.99")));
    }

    [Fact]
    public void Cart_total_is_the_exact_sum_of_line_amounts()
    {
        var cart = new Cart();
        cart.Add(new CartProduct(Guid.NewGuid(), "A", "A1", "KGM", 3, false, M("10.05")), Q("0.333"));
        cart.Add(new CartProduct(Guid.NewGuid(), "B", "B1", "KGM", 3, false, M("0.01")), Q("0.5"));

        Assert.Equal(335 + 1, cart.Total.Cents);
        Assert.Equal(cart.Lines.Sum(l => l.Amount.Cents), cart.Total.Cents);
    }
}
