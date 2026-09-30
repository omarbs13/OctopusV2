using Pos.Domain.Common;
using Pos.Domain.Inventory;

namespace Pos.Domain.Tests.Inventory;

public class StockStatusRuleTests
{
    private static Quantity Q(long units) => Quantity.FromThousandths(units * 1000);

    [Fact]
    public void Equal_to_minimum_is_low()
    {
        Assert.Equal(StockStatus.Low, StockStatusRule.Evaluate(Q(5), Q(5)));
    }

    [Fact]
    public void Zero_is_out_even_with_a_minimum()
    {
        Assert.Equal(StockStatus.Out, StockStatusRule.Evaluate(Q(0), Q(5)));
        Assert.Equal(StockStatus.Out, StockStatusRule.Evaluate(Q(0), null));
    }

    [Fact]
    public void Without_minimum_and_with_stock_is_normal()
    {
        Assert.Equal(StockStatus.Normal, StockStatusRule.Evaluate(Q(1), null));
    }

    [Fact]
    public void Above_minimum_is_normal()
    {
        Assert.Equal(StockStatus.Normal, StockStatusRule.Evaluate(Q(6), Q(5)));
    }
}
