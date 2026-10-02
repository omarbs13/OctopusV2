using Pos.Domain.Common;
using Pos.Domain.Inventory;

namespace Pos.Domain.Tests.Inventory;

public class StockAlertRuleTests
{
    private static Quantity Q(long units) => Quantity.FromThousandths(units * 1000);

    private static StockLevel S(long units) => StockLevel.FromThousandths(units * 1000);

    [Fact]
    public void Equal_to_reorder_point_is_urgent()
    {
        Assert.Equal(StockAlertLevel.Urgent, StockAlertRule.Evaluate(S(5), Q(20), Q(5)));
    }

    [Fact]
    public void Between_reorder_point_and_minimum_is_alert()
    {
        Assert.Equal(StockAlertLevel.Alert, StockAlertRule.Evaluate(S(6), Q(20), Q(5)));
        Assert.Equal(StockAlertLevel.Alert, StockAlertRule.Evaluate(S(20), Q(20), Q(5)));
    }

    [Fact]
    public void Above_minimum_is_none()
    {
        Assert.Equal(StockAlertLevel.None, StockAlertRule.Evaluate(S(21), Q(20), Q(5)));
    }

    [Fact]
    public void Without_thresholds_is_none()
    {
        Assert.Equal(StockAlertLevel.None, StockAlertRule.Evaluate(S(0), null, null));
        Assert.Equal(StockAlertLevel.None, StockAlertRule.Evaluate(S(-3), null, null));
    }

    [Fact]
    public void Zero_with_only_minimum_is_alert()
    {
        Assert.Equal(StockAlertLevel.Alert, StockAlertRule.Evaluate(S(0), Q(10), null));
    }

    [Fact]
    public void Negative_with_reorder_point_is_urgent()
    {
        Assert.Equal(StockAlertLevel.Urgent, StockAlertRule.Evaluate(S(-2), null, Q(0)));
    }
}
