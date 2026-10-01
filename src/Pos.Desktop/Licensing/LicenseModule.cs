using Microsoft.Extensions.DependencyInjection;
using Pos.Desktop.Navigation;

namespace Pos.Desktop.Licensing;

public static class LicenseModule
{
    /// <summary>Tarjeta de licencia en Inicio (011).</summary>
    public static IServiceCollection AddLicenseModule(this IServiceCollection services)
    {
        services.AddDashboardCard<LicenseCard>();
        return services;
    }
}
