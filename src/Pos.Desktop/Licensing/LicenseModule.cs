using Microsoft.Extensions.DependencyInjection;
using Pos.Desktop.About;
using Pos.Desktop.Navigation;
using Pos.Desktop.Resources;

namespace Pos.Desktop.Licensing;

public static class LicenseModule
{
    public const string PageId = "help.license";

    /// <summary>
    /// Tarjeta de licencia en Inicio y "Ayuda > Licencia" (025, blocked-mode §4): primera opción del grupo
    /// Ayuda, sin permiso de navegación, así que es visible siempre, también en bloqueo.
    /// </summary>
    public static IServiceCollection AddLicenseModule(this IServiceCollection services)
    {
        services.AddDashboardCard<LicenseCard>();
        services.AddPage<LicenseViewModel, LicenseView>(PageId, Strings.Nav_License, "Icon.Key", 0, AboutModule.GroupId);
        return services;
    }
}
