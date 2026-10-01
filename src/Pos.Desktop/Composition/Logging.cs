using System.Runtime.InteropServices;
using Pos.Desktop.Diagnostics;
using Pos.Infrastructure.Platform;
using Serilog;

namespace Pos.Desktop.Composition;

internal static class Logging
{
    /// <summary>Formato legible: fecha y hora, nivel (INFO, WARNING, ERROR, FATAL), mensaje, contexto y excepción.</summary>
    internal const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{LevelName}] {Message:lj} {Properties:j}{NewLine}{Exception}";

    /// <summary>
    /// Log de texto en archivos diarios (<c>pos-AAAAMMDD.log</c>) dentro de la carpeta de datos. La
    /// retención de 30 días la aplica <c>LogRetention</c> por fecha (constitución, Principio VIII).
    /// </summary>
    public static ILogger Create(AppPaths paths, string version, DiagnosticContext context) =>
        new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("AppVersion", version)
            .Enrich.WithProperty("MachineName", Environment.MachineName)
            .Enrich.WithProperty("OperatingSystem", RuntimeInformation.OSDescription)
            .Enrich.With(new DiagnosticContextEnricher(context))
            .Enrich.With(new LevelNameEnricher())
            .Enrich.With(new SensitiveDataRedactor())
            .WriteTo.File(
                Path.Combine(paths.LogsDirectory, "pos-.log"),
                outputTemplate: OutputTemplate,
                formatProvider: System.Globalization.CultureInfo.InvariantCulture,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: null,
                shared: true)
            .CreateLogger();
}
