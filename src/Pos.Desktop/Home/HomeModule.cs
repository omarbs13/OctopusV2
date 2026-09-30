using Microsoft.Extensions.DependencyInjection;
using Pos.Desktop.Home.Cards;
using Pos.Desktop.Navigation;
using Pos.Desktop.Resources;

namespace Pos.Desktop.Home;

public static class HomeModule
{
    public const string PageId = "home";

    /// <summary>Inicio: primera opción del menú, en el primer nivel.</summary>
    public static IServiceCollection AddHomeModule(this IServiceCollection services) =>
        services.AddPage<HomeViewModel, HomeView>(PageId, Strings.Nav_Home, "Icon.Home", 0);

    /// <summary>Gráficas de ventas en estado vacío hasta que exista el módulo de Ventas.</summary>
    public static IServiceCollection AddSalesPlaceholders(this IServiceCollection services)
    {
        services.AddDashboardCard<SalesTodayChart>();
        services.AddDashboardCard<SalesLast7DaysChart>();
        services.AddDashboardCard<TopProductsChart>();
        return services;
    }
}
