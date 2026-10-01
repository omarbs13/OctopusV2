using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pos.Application;
using Pos.Desktop.About;
using Pos.Desktop.Administration;
using Pos.Desktop.Auth;
using Pos.Desktop.CashShifts;
using Pos.Application.Users.Session;
using Pos.Desktop.Common;
using Pos.Desktop.Diagnostics;
using Pos.Desktop.Home;
using Pos.Desktop.Inventory;
using Pos.Desktop.Licensing;
using Pos.Desktop.Navigation;
using Pos.Desktop.Products;
using Pos.Desktop.Reports;
using Pos.Desktop.Sales;
using Pos.Desktop.Settings;
using Pos.Desktop.Splash;
using Pos.Desktop.Startup;
using Pos.Infrastructure;
using Pos.Infrastructure.Platform;
using Serilog;

namespace Pos.Desktop.Composition;

/// <summary>Raíz de composición: único lugar de Desktop que conoce Infrastructure.</summary>
internal static class HostBuilder
{
    public static IHost Build(
        AppPaths paths,
        ILogger logger,
        IClassicDesktopStyleApplicationLifetime lifetime,
        DiagnosticContext? diagnostics = null)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory,
            DisableDefaults = true,
        });

        ConfigureServices(builder.Services, paths, logger, lifetime, diagnostics);

        return builder.Build();
    }

    /// <summary>Registra todos los servicios de la aplicación; separado para poder validar el grafo completo en pruebas.</summary>
    internal static void ConfigureServices(
        IServiceCollection services,
        AppPaths paths,
        ILogger logger,
        IClassicDesktopStyleApplicationLifetime lifetime,
        DiagnosticContext? diagnostics = null)
    {
        services.AddLogging();
        services.AddSerilog(logger, dispose: false);
        services.AddSingleton(logger);
        services.AddSingleton(sp =>
        {
            var context = diagnostics ?? new DiagnosticContext();
            context.BindSession(sp.GetRequiredService<IUserSession>());
            return context;
        });
        services.AddSingleton(sp => ErrorEpisodeGate.CreateDefault(sp.GetRequiredService<ILogger>()));

        services.AddInfrastructure(paths);
        services.AddApplication();

        services.AddSingleton(lifetime);
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<OperationRunner>();
        services.AddSingleton<UseCases>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<ThemeService>();

        services.AddSingleton<StartupPresenter>();
        services.AddSingleton<IBrandingAssets, BrandingAssets>();
        services.AddSingleton<SplashViewModel>();

        // Navegación y módulos: cada módulo registra su menú, pantallas, vistas y tarjetas.
        services.AddAuthModule();
        services.AddNavigationCore();
        services.AddHomeModule();
        services.AddProductsModule();
        services.AddInventoryModule();
        services.AddSalesModule();
        services.AddCashShiftsModule();
        services.AddReportsModule();
        services.AddAdministrationModule();
        services.AddSettingsModule();
        services.AddLicenseModule();
        services.AddHelpModule();
    }
}
