using Microsoft.Extensions.DependencyInjection;
using Pos.Desktop.Navigation;
using Pos.Desktop.Resources;

namespace Pos.Desktop.Home;

public static class HomeModule
{
    public const string PageId = "home";

    /// <summary>Inicio: primera opción del menú, en el primer nivel.</summary>
    public static IServiceCollection AddHomeModule(this IServiceCollection services) =>
        services.AddPage<HomeViewModel, HomeView>(PageId, Strings.Nav_Home, "Icon.Home", 0);
}
