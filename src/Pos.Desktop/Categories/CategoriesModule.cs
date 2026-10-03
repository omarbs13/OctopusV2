using Microsoft.Extensions.DependencyInjection;
using Pos.Desktop.Navigation;
using Pos.Desktop.Products;
using Pos.Desktop.Resources;
using Pos.Domain.Users;

namespace Pos.Desktop.Categories;

public static class CategoriesModule
{
    public const string PageId = "catalogs.categories";

    /// <summary>
    /// Catálogos > Categorías (016, research §7): justo después de Productos, solo para quien tiene
    /// <c>ManageProducts</c> (el Administrador). Registra también el formulario y el selector de categoría.
    /// </summary>
    public static IServiceCollection AddCategoriesModule(this IServiceCollection services)
    {
        services.AddPage<CategoriesViewModel, CategoriesView>(PageId, Strings.Nav_Categories, "Icon.Shape", 1, ProductsModule.GroupId, permission: Permission.ManageCategories);

        services.AddTransient<CategoryFormViewModel>();
        services.AddScoped<Func<CategoryFormViewModel>>(sp => sp.GetRequiredService<CategoryFormViewModel>);
        services.AddComponentView<CategoryFormViewModel, CategoryFormView>();
        return services;
    }
}
