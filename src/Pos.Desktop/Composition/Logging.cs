using System.Runtime.InteropServices;
using Pos.Infrastructure.Platform;
using Serilog;
using Serilog.Formatting.Compact;

namespace Pos.Desktop.Composition;

internal static class Logging
{
    /// <summary>
    /// Log estructurado (CLEF) en archivos rotativos diarios dentro de la carpeta de datos,
    /// conservando 31 días (constitución, Principio VIII).
    /// </summary>
    public static ILogger Create(AppPaths paths, string version) =>
        new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("AppVersion", version)
            .Enrich.WithProperty("MachineName", Environment.MachineName)
            .Enrich.WithProperty("OperatingSystem", RuntimeInformation.OSDescription)
            .WriteTo.File(
                new CompactJsonFormatter(),
                Path.Combine(paths.LogsDirectory, "pos-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 31,
                shared: true)
            .CreateLogger();
}
