using Pos.Desktop.Common;
using Pos.Desktop.Resources;

namespace Pos.Desktop.Home.Cards;

/// <summary>Tarjeta de un módulo que aún no existe: estado vacío, nunca datos de ejemplo (FR-007, FR-008).</summary>
public abstract class ComingSoonCard : DashboardCard
{
    private readonly string _message;

    protected ComingSoonCard(OperationRunner runner, string message)
        : base(runner) => _message = message;

    protected override Task LoadCoreAsync()
    {
        SetEmpty(_message);
        return Task.CompletedTask;
    }
}

public sealed class LowStockCard(OperationRunner runner) : ComingSoonCard(runner, Strings.Card_InventoryPending)
{
    public override string Title => Strings.Card_LowStock;

    public override string Icon => "Icon.Warning";

    public override int Order => 20;

    public override DashboardCardKind Kind => DashboardCardKind.Metric;
}

public sealed class OutOfStockCard(OperationRunner runner) : ComingSoonCard(runner, Strings.Card_InventoryPending)
{
    public override string Title => Strings.Card_OutOfStock;

    public override string Icon => "Icon.Stock";

    public override int Order => 30;

    public override DashboardCardKind Kind => DashboardCardKind.Metric;
}

public sealed class SalesTodayChart(OperationRunner runner) : ComingSoonCard(runner, Strings.Card_SalesPending)
{
    public override string Title => Strings.Chart_SalesToday;

    public override string Icon => "Icon.Sales";

    public override int Order => 110;

    public override DashboardCardKind Kind => DashboardCardKind.Chart;
}

public sealed class SalesLast7DaysChart(OperationRunner runner) : ComingSoonCard(runner, Strings.Card_SalesPending)
{
    public override string Title => Strings.Chart_SalesLast7Days;

    public override string Icon => "Icon.Chart";

    public override int Order => 120;

    public override DashboardCardKind Kind => DashboardCardKind.Chart;
}

public sealed class TopProductsChart(OperationRunner runner) : ComingSoonCard(runner, Strings.Card_SalesPending)
{
    public override string Title => Strings.Chart_TopProducts;

    public override string Icon => "Icon.Product";

    public override int Order => 130;

    public override DashboardCardKind Kind => DashboardCardKind.Chart;
}
