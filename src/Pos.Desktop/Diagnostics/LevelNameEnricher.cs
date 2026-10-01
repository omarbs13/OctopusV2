using Serilog.Core;
using Serilog.Events;

namespace Pos.Desktop.Diagnostics;

/// <summary>Agrega <c>LevelName</c> con los niveles del contrato: INFO, WARNING, ERROR y FATAL.</summary>
internal sealed class LevelNameEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        var name = logEvent.Level switch
        {
            LogEventLevel.Fatal => "FATAL",
            LogEventLevel.Error => "ERROR",
            LogEventLevel.Warning => "WARNING",
            _ => "INFO",
        };
        logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("LevelName", name));
    }
}
