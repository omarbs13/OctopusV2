namespace Pos.Application.Diagnostics.ExportDiagnostics;

/// <param name="DestinationFilePath">Archivo .zip que se creará.</param>
/// <param name="IncludeDatabase">Incluye una copia de la base; por omisión no se incluye (FR-009).</param>
public sealed record ExportDiagnosticsCommand(string DestinationFilePath, bool IncludeDatabase = false);

/// <param name="DestinationFilePath">Archivo creado.</param>
/// <param name="LogFileCount">Archivos de log incluidos; cero indica que no había registros.</param>
public sealed record ExportDiagnosticsResult(string DestinationFilePath, int LogFileCount);
