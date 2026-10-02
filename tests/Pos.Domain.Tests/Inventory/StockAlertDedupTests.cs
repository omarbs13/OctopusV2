using Pos.Domain.Inventory;

namespace Pos.Domain.Tests.Inventory;

public class StockAlertDedupTests
{
    private static HashSet<StockAlertLevel> Acks(params StockAlertLevel[] levels) => [.. levels];

    [Fact]
    public void Without_acknowledgements_alert_and_urgent_are_pending()
    {
        Assert.True(StockAlertDedup.IsPending(StockAlertLevel.Alert, Acks()));
        Assert.True(StockAlertDedup.IsPending(StockAlertLevel.Urgent, Acks()));
    }

    [Fact]
    public void Escalation_from_alert_to_urgent_is_pending()
    {
        Assert.True(StockAlertDedup.IsPending(StockAlertLevel.Urgent, Acks(StockAlertLevel.Alert)));
    }

    [Fact]
    public void Descent_from_urgent_to_alert_is_not_pending()
    {
        Assert.False(StockAlertDedup.IsPending(StockAlertLevel.Alert, Acks(StockAlertLevel.Urgent)));
    }

    [Fact]
    public void Same_level_again_is_not_pending()
    {
        Assert.False(StockAlertDedup.IsPending(StockAlertLevel.Alert, Acks(StockAlertLevel.Alert)));
        Assert.False(StockAlertDedup.IsPending(StockAlertLevel.Urgent, Acks(StockAlertLevel.Urgent)));
    }

    [Fact]
    public void None_is_never_pending()
    {
        Assert.False(StockAlertDedup.IsPending(StockAlertLevel.None, Acks()));
    }
}
