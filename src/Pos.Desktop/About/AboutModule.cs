using Microsoft.Extensions.DependencyInjection;
using Pos.Desktop.Navigation;
using Pos.Desktop.Resources;

namespace Pos.Desktop.About;

public static class AboutModule
{
    public const string GroupId = "help";
    public const string PageId = "help.about";

    /// <summary>
    /// Grupo Ayuda con la pantalla Acerca de ("Probar escáner" está en Configuración, 023). "Licencia" (025) es la
    /// primera opción del grupo; "Acerca de" va después.
    /// </summary>
    public static IServiceCollection AddHelpModule(this IServiceCollection services)
    {
        services.AddNavigationGroup(GroupId, Strings.Nav_Help, "Icon.Help", 90);
        services.AddPage<AboutViewModel, AboutView>(PageId, Strings.Nav_About, "Icon.Info", 1, GroupId);
        return services;
    }
}
