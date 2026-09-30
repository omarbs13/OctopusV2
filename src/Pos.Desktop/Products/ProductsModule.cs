using Microsoft.Extensions.DependencyInjection;
using Pos.Desktop.Home.Cards;
using Pos.Desktop.Navigation;
using Pos.Desktop.Resources;
using Pos.Domain.Users;

namespace Pos.Desktop.Products;

public static class ProductsModule
{
    public const string GroupId = "catalogs";
    public const string PageId = "catalogs.products";

    /// <summary>Grupo Catálogos, pantalla Productos y su formulario.</summary>
    public static IServiceCollection AddProductsModule(this IServiceCollection services)
    {
        services.AddNavigationGroup(GroupId, Strings.Nav_Catalogs, "Icon.Catalog", 10);
        services.AddPage<ProductsViewModel, ProductsView>(PageId, Strings.Nav_Products, "Icon.Product", 0, GroupId, permission: Permission.ViewProducts);

        services.AddTransient<ProductEditorViewModel>();
        services.AddScoped<Func<ProductEditorViewModel>>(sp => sp.GetRequiredService<ProductEditorViewModel>);
        services.AddComponentView<ProductEditorViewModel, ProductEditorView>();
        services.AddDashboardCard<ActiveProductsCard>();
        return services;
    }
}
