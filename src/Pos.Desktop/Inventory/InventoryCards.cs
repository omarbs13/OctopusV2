using System.Globalization;
using Pos.Application.Abstractions;
using Pos.Application.Inventory;
using Pos.Application.Inventory.GetStockAlerts;
using Pos.Desktop.Common;
using Pos.Desktop.Home;
using Pos.Desktop.Resources;
using Pos.Domain.Users;

namespace Pos.Desktop.Inventory;

/// <summary>Productos activos con existencia baja; lleva a Existencias ya filtrada (FR-021).</summary>
public sealed class LowStockCard(OperationRunner runner, UseCases useCases) : DashboardCard(runner)
{
    public override string Title => Strings.Card_LowStock;

    public override string Icon => "Icon.Warning";

    public override int Order => 20;

    public override Permission? RequiredPermission => Permission.ViewInventory;

    public override DashboardCardKind Kind => DashboardCardKind.Metric;

    public override string? NavigateTo => InventoryModule.StockPageId;

    public override object? NavigationArgument => StockFilter.Low;

    protected override async Task LoadCoreAsync()
    {
        var counts = await InventoryCardCounts.LoadAsync(useCases);
        SetReady(counts.Low.ToString("N0", InventoryCardCounts.DisplayCulture));
    }
}

/// <summary>Productos activos sin existencia; lleva a Existencias ya filtrada (FR-021).</summary>
public sealed class OutOfStockCard(OperationRunner runner, UseCases useCases) : DashboardCard(runner)
{
    public override string Title => Strings.Card_OutOfStock;

    public override string Icon => "Icon.Stock";

    public override int Order => 30;

    public override Permission? RequiredPermission => Permission.ViewInventory;

    public override DashboardCardKind Kind => DashboardCardKind.Metric;

    public override string? NavigateTo => InventoryModule.StockPageId;

    public override object? NavigationArgument => StockFilter.Out;

    protected override async Task LoadCoreAsync()
    {
        var counts = await InventoryCardCounts.LoadAsync(useCases);
        SetReady(counts.Out.ToString("N0", InventoryCardCounts.DisplayCulture));
    }
}

internal static class InventoryCardCounts
{
    public static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("es-MX");

    public static async Task<StockAlertCounts> LoadAsync(UseCases useCases) =>
        (await useCases.RunAsync<GetStockAlertsHandler, Result<StockAlertCounts>>(
            h => h.HandleAsync(CancellationToken.None))).Value;
}
