using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pos.Application;
using Pos.Desktop.About;
using Pos.Desktop.Common;
using Pos.Desktop.Products;
using Pos.Desktop.Shell;
using Pos.Desktop.Startup;
using Pos.Infrastructure;
using Pos.Infrastructure.Platform;
using Serilog;

namespace Pos.Desktop.Composition;

/// <summary>Raíz de composición: único lugar de Desktop que conoce Infrastructure.</summary>
internal static class HostBuilder
{
    public static IHost Build(AppPaths paths, ILogger logger, IClassicDesktopStyleApplicationLifetime lifetime)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory,
            DisableDefaults = true,
        });

        builder.Services.AddSerilog(logger, dispose: false);
        builder.Services.AddSingleton(logger);

        builder.Services.AddInfrastructure(paths);
        builder.Services.AddApplication();

        builder.Services.AddSingleton(lifetime);
        builder.Services.AddSingleton<IDialogService, DialogService>();
        builder.Services.AddSingleton<OperationRunner>();
        builder.Services.AddSingleton<UseCases>();
        builder.Services.AddSingleton<IClipboardService, ClipboardService>();

        builder.Services.AddSingleton<StartupPresenter>();
        builder.Services.AddTransient<ProductEditorViewModel>();
        builder.Services.AddSingleton<Func<ProductEditorViewModel>>(sp => sp.GetRequiredService<ProductEditorViewModel>);
        builder.Services.AddSingleton<ProductsViewModel>();
        builder.Services.AddSingleton<AboutViewModel>();

        // Orden del menú lateral: Productos, Acerca de.
        builder.Services.AddSingleton<PageViewModel>(sp => sp.GetRequiredService<ProductsViewModel>());
        builder.Services.AddSingleton<PageViewModel>(sp => sp.GetRequiredService<AboutViewModel>());

        builder.Services.AddSingleton<MainViewModel>();

        return builder.Build();
    }
}
