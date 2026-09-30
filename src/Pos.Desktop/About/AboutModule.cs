using Microsoft.Extensions.DependencyInjection;
using Pos.Desktop.Navigation;
using Pos.Desktop.Resources;

namespace Pos.Desktop.About;

public static class AboutModule
{
    public const string GroupId = "help";
    public const string PageId = "help.about";

    /// <summary>Grupo Ayuda y pantalla Acerca de.</summary>
    public static IServiceCollection AddHelpModule(this IServiceCollection services)
    {
        services.AddNavigationGroup(GroupId, Strings.Nav_Help, "Icon.Help", 90);
        services.AddPage<AboutViewModel, AboutView>(PageId, Strings.Nav_About, "Icon.Info", 0, GroupId);
        return services;
    }
}
