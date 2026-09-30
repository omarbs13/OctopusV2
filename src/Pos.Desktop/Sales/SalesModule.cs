using Microsoft.Extensions.DependencyInjection;
using Pos.Desktop.Navigation;
using Pos.Desktop.Resources;
using Pos.Domain.Users;

namespace Pos.Desktop.Sales;

public static class SalesModule
{
    public const string GroupId = "sales";
    public const string PointOfSalePageId = "sales.pos";
    public const string HistoryPageId = "sales.history";

    /// <summary>Ventas: menú, Punto de venta, cobro y selector de productos.</summary>
    public static IServiceCollection AddSalesModule(this IServiceCollection services)
    {
        services.AddNavigationGroup(GroupId, Strings.Nav_Sales, "Icon.Sales", 5);
        services.AddPage<PointOfSaleViewModel, PointOfSaleView>(PointOfSalePageId, Strings.Nav_PointOfSale, "Icon.Sales", 0, GroupId, shortcut: "F9", permission: Permission.Sell);

        services.AddPage<SalesHistoryViewModel, SalesHistoryView>(HistoryPageId, Strings.Nav_SalesHistory, "Icon.Movements", 10, GroupId, permission: Permission.ViewOwnSales);

        services.AddComponentView<ProductChooserViewModel, ProductChooserView>();
        services.AddTransient<SaleDetailViewModel>();
        services.AddScoped<Func<SaleDetailViewModel>>(sp => sp.GetRequiredService<SaleDetailViewModel>);
        services.AddComponentView<SaleDetailViewModel, SaleDetailView>();
        services.AddComponentView<CancelSaleViewModel, CancelSaleView>();
        services.AddComponentView<CheckoutViewModel, CheckoutView>();

        // Tarjetas de Inicio: comparten una sola consulta por activación.
        services.AddScoped<SalesDashboardSource>();
        services.AddDashboardCard<SalesTodayChart>();
        services.AddDashboardCard<SalesLast7DaysChart>();
        services.AddDashboardCard<TopProductsChart>();
        return services;
    }
}
