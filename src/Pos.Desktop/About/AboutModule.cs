using Microsoft.Extensions.DependencyInjection;
using Pos.Desktop.Navigation;
using Pos.Desktop.Resources;

namespace Pos.Desktop.About;

public static class AboutModule
{
    public const string GroupId = "help";
    public const string PageId = "help.about";
    public const string ScannerTestPageId = "help.scanner-test";

    /// <summary>Grupo Ayuda, pantalla Acerca de y "Probar escáner" (021, visible para todos los roles).</summary>
    public static IServiceCollection AddHelpModule(this IServiceCollection services)
    {
        services.AddNavigationGroup(GroupId, Strings.Nav_Help, "Icon.Help", 90);
        services.AddPage<AboutViewModel, AboutView>(PageId, Strings.Nav_About, "Icon.Info", 0, GroupId);
        services.AddPage<ScannerTestViewModel, ScannerTestView>(ScannerTestPageId, Strings.Nav_ScannerTest, "Icon.Product", 10, GroupId);
        return services;
    }
}
