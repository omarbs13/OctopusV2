using Microsoft.Extensions.DependencyInjection;
using Pos.Desktop.Navigation;
using Pos.Desktop.Resources;
using Pos.Domain.Users;

namespace Pos.Desktop.Inventory;

public static class InventoryModule
{
    public const string GroupId = "inventory";
    public const string StockPageId = "inventory.stock";
    public const string MovementsPageId = "inventory.movements";

    /// <summary>Inventario: menú, pantallas Existencias y Movimientos, formulario de movimiento y tarjetas de Inicio.</summary>
    public static IServiceCollection AddInventoryModule(this IServiceCollection services)
    {
        services.AddNavigationGroup(GroupId, Strings.Nav_Inventory, "Icon.Inventory", 20);
        services.AddPage<StockViewModel, StockView>(StockPageId, Strings.Nav_Stock, "Icon.Stock", 0, GroupId, permission: Permission.ViewInventory);
        services.AddPage<MovementsViewModel, MovementsView>(MovementsPageId, Strings.Nav_Movements, "Icon.Movements", 10, GroupId, permission: Permission.ViewInventory);

        services.AddTransient<MovementEditorViewModel>();
        services.AddScoped<Func<MovementEditorViewModel>>(sp => sp.GetRequiredService<MovementEditorViewModel>);
        services.AddComponentView<MovementEditorViewModel, MovementEditorView>();

        // Revisión de alertas de existencia de la sesión (022).
        services.AddScoped<StockAlertMonitor>();

        services.AddDashboardCard<StockAlertLevelsCard>();
        services.AddDashboardCard<OutOfStockCard>();
        return services;
    }
}
