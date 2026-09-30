using Microsoft.Extensions.DependencyInjection;
using Pos.Desktop.Home.Cards;
using Pos.Desktop.Navigation;
using Pos.Desktop.Resources;

namespace Pos.Desktop.Inventory;

public static class InventoryModule
{
    public const string GroupId = "inventory";

    /// <summary>
    /// Inventario: menú y tarjetas de Inicio. Hasta que exista el módulo, sus opciones muestran
    /// "disponible más adelante" y sus tarjetas un estado vacío.
    /// </summary>
    public static IServiceCollection AddInventoryModule(this IServiceCollection services)
    {
        services.AddNavigationGroup(GroupId, Strings.Nav_Inventory, "Icon.Inventory", 20);
        services.AddComingSoonPage("inventory.stock", Strings.Nav_Stock, "Icon.Stock", 0, GroupId);
        services.AddComingSoonPage("inventory.movements", Strings.Nav_Movements, "Icon.Movements", 10, GroupId);

        services.AddDashboardCard<LowStockCard>();
        services.AddDashboardCard<OutOfStockCard>();
        return services;
    }
}
