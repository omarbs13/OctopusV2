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
    public const string ReceivablesPageId = "reports.receivables";
    public const string PurchasesPageId = "reports.purchases";

    /// <summary>
    /// Reportes y análisis (009): grupo "Reportes" con Ventas, Arqueo e Inventario, la vista "Mi turno"
    /// del Cajero y la tarjeta de alertas de Inicio. Cada historia agrega sus páginas aquí.
    /// </summary>
    public static IServiceCollection AddReportsModule(this IServiceCollection services)
    {
        services.AddScoped<ReportExportCoordinator>();
        services.AddNavigationGroup(GroupId, Strings.Nav_Reports, "Icon.Chart", 7);

        services.AddPage<SalesReportViewModel, SalesReportView>(SalesPageId, Strings.Nav_ReportSales, "Icon.ChartLine", 0, GroupId, permission: Permission.ViewReports);

        services.AddPage<CashCountReportViewModel, CashCountReportView>(CashCountPageId, Strings.Nav_ReportCashCount, "Icon.CashCheck", 10, GroupId, permission: Permission.ViewReports);

        services.AddPage<InventoryReportViewModel, InventoryReportView>(InventoryPageId, Strings.Nav_ReportInventory, "Icon.ClipboardList", 20, GroupId, permission: Permission.ViewInventory);

        // "Compras" (020): depende de la licencia Inventario (vía ViewPurchaseReport), entre Inventario y Créditos.
        services.AddPage<PurchaseReportViewModel, PurchaseReportView>(PurchasesPageId, Strings.Nav_ReportPurchases, "Icon.TruckCheck", 25, GroupId, permission: Permission.ViewPurchaseReport);

        // "Créditos" (014): depende de la licencia Crédito y clientes (vía ViewReceivables), no de Reportes avanzados.
        services.AddPage<ReceivablesReportViewModel, ReceivablesReportView>(ReceivablesPageId, Strings.Nav_ReportReceivables, "Icon.AccountCash", 30, GroupId, permission: Permission.ViewReceivables);

        // "Mi turno": dentro del grupo Ventas, junto a "Turnos" (contracts/ui.md).
        services.AddPage<MyShiftViewModel, MyShiftView>(MyShiftPageId, Strings.Nav_MyShift, "Icon.AccountClock", 15, SalesModule.GroupId, permission: Permission.OperateShift);

        services.AddComponentView<PeriodPickerViewModel, PeriodPickerView>();
        services.AddComponentView<ChartViewModel, ChartImageView>();

        // Tarjeta de Inicio (Historia 7): solo con ViewReports.
        services.AddDashboardCard<AlertsCard>();
        return services;
    }
}
