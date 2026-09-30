using Avalonia;
using Pos.Infrastructure.Diagnostics;
using Pos.Infrastructure.Platform;
using Pos.Infrastructure.Startup;
using Serilog;

namespace Pos.Desktop.Composition;

internal static class Program
{
    /// <summary>Contexto preparado antes de iniciar Avalonia.</summary>
    internal static StartupContext Context { get; private set; } = null!;

    [STAThread]
    public static int Main(string[] args)
    {
        var paths = AppPaths.FromEnvironment();
        TryEnsureDirectories(paths);

        var logger = Logging.Create(paths, new AssemblyAppInfo().Version);
        Log.Logger = logger;

        // 1. Instancia única (primer paso obligatorio de la secuencia de arranque).
        var guard = TryAcquireGuard(paths, logger);
        var alreadyRunning = guard is null && File.Exists(paths.LockFile);
        if (alreadyRunning)
        {
            logger.Information("Ya hay una instancia en ejecución; se le pide traer su ventana al frente");
            _ = SingleInstanceGuard
                .TrySignalExistingAsync(SingleInstanceGuard.DefaultPipeName, TimeSpan.FromSeconds(2))
                .GetAwaiter()
                .GetResult();
        }
        else
        {
            logger.Information("Iniciando POS en {DataDirectory}", paths.DataDirectory);
        }

        Context = new StartupContext(paths, logger, guard, alreadyRunning);
        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            guard?.Dispose();
            Log.CloseAndFlush();
        }
    }

    // Configuración de Avalonia; también la usa el diseñador visual.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static SingleInstanceGuard? TryAcquireGuard(AppPaths paths, ILogger logger)
    {
        try
        {
            return SingleInstanceGuard.TryAcquire(paths.LockFile, SingleInstanceGuard.DefaultPipeName);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            // Sin permisos en la carpeta de datos: el arranque de la base lo informará al operador.
            logger.Warning(ex, "No se pudo crear el archivo de bloqueo {LockFile}", paths.LockFile);
            return null;
        }
    }

    private static void TryEnsureDirectories(AppPaths paths)
    {
        try
        {
            paths.EnsureDirectories();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // El arranque detectará la falta de permisos y lo informará al operador.
        }
    }
}

internal sealed record StartupContext(AppPaths Paths, ILogger Logger, SingleInstanceGuard? Guard, bool AlreadyRunning);
