using Pos.Application.Inventory;
using Pos.Desktop.Common;
using Pos.Desktop.Home;
using Pos.Desktop.Reports;
using Pos.Desktop.Resources;
using Pos.Domain.Users;

namespace Pos.Desktop.Inventory;

/// <summary>
/// "Alertas de existencia" (022, FR-018): productos activos urgentes y en alerta, cada cifra con su color
/// y navegable al reporte de inventario ya filtrado. Reemplaza a "Existencia baja"; se recalcula cada vez
/// que se muestra Inicio (FR-019).
/// </summary>
public sealed class StockAlertLevelsCard(OperationRunner runner, UseCases useCases) : DashboardCard(runner)
{
    public override string Title => Strings.Card_StockAlertLevels;

    public override string Icon => "Icon.Warning";

    public override int Order => 20;

    public override Permission? RequiredPermission => Permission.ViewInventory;

    public override DashboardCardKind Kind => DashboardCardKind.Metric;

    protected override async Task LoadCoreAsync()
    {
        var counts = await InventoryCardCounts.LoadAsync(useCases);
        Segments =
        [
            Segment(Strings.Card_StockAlertLevelsUrgent, counts.Urgent, DashboardSegmentTone.Danger, StockFilter.Urgent),
            Segment(Strings.Card_StockAlertLevelsAlert, counts.Alert, DashboardSegmentTone.Warning, StockFilter.Alert),
        ];
        SetReady(null);
    }

    private static DashboardCardSegment Segment(string label, long count, DashboardSegmentTone tone, StockFilter filter) => new(
        label,
        count.ToString("N0", InventoryCardCounts.DisplayCulture),
        count > 0 ? tone : DashboardSegmentTone.Neutral,
        ReportsModule.InventoryPageId,
        filter);
}
