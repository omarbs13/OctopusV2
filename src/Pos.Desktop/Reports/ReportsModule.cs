using Microsoft.Extensions.DependencyInjection;
using Pos.Desktop.Navigation;
using Pos.Desktop.Resources;
using Pos.Desktop.Sales;
using Pos.Domain.Users;

namespace Pos.Desktop.Reports;

public static class ReportsModule
{
    public const string GroupId = "reports";
    public const string SalesPageId = "reports.sales";
    public const string CashCountPageId = "reports.cashcount";
    public const string InventoryPageId = "reports.inventory";
    public const string MyShiftPageId = "sales.myshift";

    /// <summary>
    /// Reportes y análisis (009): grupo "Reportes" con Ventas, Arqueo e Inventario, la vista "Mi turno"
    /// del Cajero y la tarjeta de alertas de Inicio. Cada historia agrega sus páginas aquí.
    /// </summary>
    public static IServiceCollection AddReportsModule(this IServiceCollection services)
    {
        services.AddScoped<ReportExportCoordinator>();
        services.AddNavigationGroup(GroupId, Strings.Nav_Reports, "Icon.Chart", 7);

        services.AddPage<SalesReportViewModel, SalesReportView>(SalesPageId, Strings.Nav_ReportSales, "Icon.Chart", 0, GroupId, permission: Permission.ViewReports);

        services.AddPage<CashCountReportViewModel, CashCountReportView>(CashCountPageId, Strings.Nav_ReportCashCount, "Icon.Movements", 10, GroupId, permission: Permission.ViewReports);

        services.AddPage<InventoryReportViewModel, InventoryReportView>(InventoryPageId, Strings.Nav_ReportInventory, "Icon.Stock", 20, GroupId, permission: Permission.ViewInventory);

        // "Mi turno": dentro del grupo Ventas, junto a "Turnos" (contracts/ui.md).
        services.AddPage<MyShiftViewModel, MyShiftView>(MyShiftPageId, Strings.Nav_MyShift, "Icon.Movements", 15, SalesModule.GroupId, permission: Permission.OperateShift);

        services.AddComponentView<PeriodPickerViewModel, PeriodPickerView>();
        services.AddComponentView<ChartViewModel, ChartImageView>();

        // Tarjeta de Inicio (Historia 7): solo con ViewReports.
        services.AddDashboardCard<AlertsCard>();
        return services;
    }
}
