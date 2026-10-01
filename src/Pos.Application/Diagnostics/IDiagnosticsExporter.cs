namespace Pos.Application.Diagnostics;

/// <summary>Crea el paquete de diagnóstico para soporte técnico.</summary>
public interface IDiagnosticsExporter
{
    /// <summary>
    /// Escribe en <paramref name="destinationFile"/> un zip con los logs de los últimos 30 días y la
    /// información de la aplicación; el respaldo de la base solo va si <paramref name="includeDatabase"/>.
    /// Nunca deja un archivo incompleto. Devuelve la cantidad de archivos de log incluidos.
    /// </summary>
    Task<int> ExportAsync(string destinationFile, bool includeDatabase, CancellationToken cancellationToken);
}
