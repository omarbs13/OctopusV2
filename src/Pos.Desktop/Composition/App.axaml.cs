using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pos.Application.Startup;
using Pos.Desktop.Common;
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
                _host = HostBuilder.Build(context.Paths, context.Logger, desktop);

                // Las vistas se resuelven con lo que registró cada módulo.
                DataTemplates.Add(_host.Services.GetRequiredService<RegisteredViewLocator>());
                GlobalExceptionHandlers.Register(context.Logger, () => _host?.Services.GetService<IDialogService>());
                desktop.Exit += (_, _) =>
                {
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

        await splashViewModel.WaitMinimumAsync();

        var main = services.GetRequiredService<MainViewModel>();
        var window = new MainWindow { DataContext = main };
        window.Closing += (_, e) => OnMainWindowClosing(window, main, e, services.GetRequiredService<IDatabaseStartup>());

        if (context.Guard is not null)
        {
            context.Guard.ActivationRequested += (_, _) => Dispatcher.UIThread.Post(window.BringToFront);
        }

        await main.StartAsync();
        desktop.MainWindow = window;
        window.Show();
        splash.Close();
        desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
    }

    /// <summary>
    /// Al cerrar: primero se confirma salir de un formulario con cambios (Seguir editando cancela
    /// el cierre, sin respaldo); después, respaldo automático con límite de tiempo y cierre.
    /// </summary>
    private void OnMainWindowClosing(Window window, MainViewModel main, WindowClosingEventArgs e, IDatabaseStartup startup)
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
            if (!await main.CanCloseAsync())
            {
                _closing = false;
                return;
            }

            window.IsEnabled = false;
            await Task.Run(startup.BackupOnCloseAsync);
            _closeBackupDone = true;
            window.Close();
        });
    }
}
