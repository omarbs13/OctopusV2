using Microsoft.Extensions.DependencyInjection;
using Pos.Desktop.Inventory;
using Pos.Desktop.Navigation;
using Pos.Desktop.Resources;
using Pos.Domain.Users;

namespace Pos.Desktop.Purchases;

public static class PurchasesModule
{
    public const string PurchaseEntryPageId = "inventory.purchases";
    public const string SuppliersPageId = "inventory.suppliers";

    /// <summary>
    /// Proveedores y compras (020): páginas del grupo "Inventario". Sus permisos son del módulo
    /// <c>Inventory</c>, así que desaparecen del menú sin licencia (contracts/ui.md "Navegación").
    /// </summary>
    public static IServiceCollection AddPurchasesModule(this IServiceCollection services)
    {
        services.AddPage<PurchaseEntryViewModel, PurchaseEntryView>(PurchaseEntryPageId, Strings.Nav_PurchaseEntry, "Icon.TruckDelivery", 20, InventoryModule.GroupId, permission: Permission.RegisterPurchases);
        services.AddPage<SupplierListViewModel, SupplierListView>(SuppliersPageId, Strings.Nav_Suppliers, "Icon.Factory", 30, InventoryModule.GroupId, permission: Permission.ManageSuppliers);

        services.AddTransient<SupplierFormViewModel>();
        services.AddScoped<Func<SupplierFormViewModel>>(sp => sp.GetRequiredService<SupplierFormViewModel>);
        services.AddComponentView<SupplierFormViewModel, SupplierFormView>();

        services.AddTransient<PurchaseDetailViewModel>();
        services.AddScoped<Func<PurchaseDetailViewModel>>(sp => sp.GetRequiredService<PurchaseDetailViewModel>);
        services.AddComponentView<PurchaseDetailViewModel, PurchaseDetailView>();
        services.AddComponentView<VoidPurchaseViewModel, VoidPurchaseView>();
        return services;
    }
}
