using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pos.Application.Licensing;
using Pos.Application.Startup;
using Pos.Desktop.Common;
using Pos.Desktop.Diagnostics;
using Pos.Desktop.Navigation;
using Pos.Desktop.Resources;
using Pos.Desktop.Shell;
using Pos.Desktop.Splash;
using Pos.Desktop.Startup;
using Serilog;

namespace Pos.Desktop.Composition;

public partial class App : Avalonia.Application
{
    private IHost? _host;
    private bool _closeBackupDone;
    private bool _closing;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Hasta abrir la ventana principal, los diálogos de arranque no deben cerrar la aplicación.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var context = Program.Context;

            if (context.AlreadyRunning)
            {
                Dispatcher.UIThread.Post(async () =>
                {
                    await new DialogService(desktop).ShowMessageAsync(Strings.Startup_Title, Strings.Startup_AlreadyRunning);
                    desktop.Shutdown();
                });
            }
            else
            {
                _host = HostBuilder.Build(context.Paths, context.Logger, desktop, context.Diagnostics);

                // El tema guardado se aplica antes de mostrar la pantalla de carga.
                _host.Services.GetRequiredService<ThemeService>().ApplySaved();

                // Las vistas se resuelven con lo que registró cada módulo.
                DataTemplates.Add(_host.Services.GetRequiredService<RegisteredViewLocator>());
                GlobalExceptionHandlers.Register(
                    context.Logger,
                    _host.Services.GetRequiredService<ErrorEpisodeGate>(),
                    _host.Services.GetRequiredService<DiagnosticContext>(),
                    () => _host?.Services.GetService<IDialogService>());
                var retention = new LogRetentionScheduler(context.Paths.LogsDirectory);
                var licenseClock = new LicenseClockScheduler(_host.Services.GetRequiredService<LicenseBootstrapper>());
                desktop.Exit += (_, _) =>
                {
                    retention.Dispose();
                    licenseClock.Dispose();
                    _host?.Dispose();
                    Log.CloseAndFlush();
                };
                Dispatcher.UIThread.Post(() => _ = StartAsync(desktop, _host.Services, context));
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task StartAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        IServiceProvider services,
        StartupContext context)
    {
        // Pantalla de carga: es la dueña de los diálogos de arranque hasta que abre la principal.
        var splashViewModel = services.GetRequiredService<SplashViewModel>();
        var splash = new SplashWindow { DataContext = splashViewModel };
        desktop.MainWindow = splash;
        splash.Show();

        var presenter = services.GetRequiredService<StartupPresenter>();
        if (!await presenter.RunAsync(CancellationToken.None, splashViewModel))
        {
            splash.Close();
            desktop.Shutdown(1);
            return;
        }

        await services.GetRequiredService<LicenseBootstrapper>().RunAsync(CancellationToken.None);
        await splashViewModel.WaitMinimumAsync();

        var root = services.GetRequiredService<RootViewModel>();
        var window = new MainWindow { DataContext = root };
        services.GetRequiredService<IdleMonitor>().Attach(window);
        window.Closing += (_, e) => OnMainWindowClosing(window, root, e, services.GetRequiredService<IDatabaseStartup>());

        if (context.Guard is not null)
        {
            context.Guard.ActivationRequested += (_, _) => Dispatcher.UIThread.Post(window.BringToFront);
        }

        await root.StartAsync();
        desktop.MainWindow = window;
        window.Show();
        splash.Close();
        desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
    }

    /// <summary>
    /// Al cerrar: primero se confirma salir de un formulario con cambios (Seguir editando cancela
    /// el cierre, sin respaldo); después se audita el cierre de sesión, respaldo automático con
    /// límite de tiempo y cierre.
    /// </summary>
    private void OnMainWindowClosing(Window window, RootViewModel root, WindowClosingEventArgs e, IDatabaseStartup startup)
    {
        if (_closeBackupDone)
        {
            return;
        }

        e.Cancel = true;
        if (_closing)
        {
            return;
        }

        _closing = true;
        Dispatcher.UIThread.Post(async () =>
        {
            if (!await root.CanCloseAsync())
            {
                _closing = false;
                return;
            }

            window.IsEnabled = false;
            await root.EndSessionOnCloseAsync();
            await Task.Run(startup.BackupOnCloseAsync);
            _closeBackupDone = true;
            window.Close();
        });
    }
}
