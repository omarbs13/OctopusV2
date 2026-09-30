namespace Pos.Application.Diagnostics;

/// <summary>Crea el paquete de diagnóstico para soporte técnico.</summary>
public interface IDiagnosticsExporter
{
    /// <summary>
    /// Escribe en <paramref name="destinationFile"/> un zip con los logs recientes, un respaldo
    /// consistente de la base y la información de la aplicación. Nunca deja un archivo incompleto.
    /// </summary>
    Task ExportAsync(string destinationFile, CancellationToken cancellationToken);
}
