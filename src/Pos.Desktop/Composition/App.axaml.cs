using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pos.Application.Startup;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;
using Pos.Desktop.Shell;
using Pos.Desktop.Startup;
using Serilog;

namespace Pos.Desktop.Composition;

public partial class App : Avalonia.Application
{
    private IHost? _host;
    private bool _closeBackupDone;

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
        var presenter = services.GetRequiredService<StartupPresenter>();
        if (!await presenter.RunAsync(CancellationToken.None))
        {
            desktop.Shutdown(1);
            return;
        }

        var window = new MainWindow
        {
            DataContext = services.GetRequiredService<MainViewModel>(),
        };
        window.Closing += (_, e) => OnMainWindowClosing(window, e, services.GetRequiredService<IDatabaseStartup>());

        if (context.Guard is not null)
        {
            context.Guard.ActivationRequested += (_, _) => Dispatcher.UIThread.Post(window.BringToFront);
        }

        desktop.MainWindow = window;
        desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
        window.Show();
    }

    /// <summary>Respaldo automático al cerrar (si el último tiene más de 24 h), con límite de tiempo.</summary>
    private void OnMainWindowClosing(Window window, WindowClosingEventArgs e, IDatabaseStartup startup)
    {
        if (_closeBackupDone)
        {
            return;
        }

        e.Cancel = true;
        window.IsEnabled = false;
        _ = Task.Run(startup.BackupOnCloseAsync).ContinueWith(
            _ => Dispatcher.UIThread.Post(() =>
            {
                _closeBackupDone = true;
                window.Close();
            }),
            TaskScheduler.Default);
    }
}
