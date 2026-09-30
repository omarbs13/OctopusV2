using Pos.Application.Abstractions;
using Pos.Application.Startup;

namespace Pos.Application.Diagnostics.ExportDiagnostics;

public sealed class ExportDiagnosticsHandler
{
    public const string DestinationField = "DestinationFilePath";
    public const string DestinationRequiredMessage = "Elija dónde guardar el archivo de diagnóstico.";
    public const string ExportFailedMessage =
        "No se pudo exportar el diagnóstico. Verifique que la ubicación elegida tenga espacio y permisos de escritura.";

    private readonly IDiagnosticsExporter _exporter;

    public ExportDiagnosticsHandler(IDiagnosticsExporter exporter) => _exporter = exporter;

    public async Task<Result<string>> HandleAsync(ExportDiagnosticsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.DestinationFilePath))
        {
            return Result.Failure<string>(new ValidationFailed([new FieldError(DestinationField, DestinationRequiredMessage)]));
        }

        try
        {
            await _exporter.ExportAsync(command.DestinationFilePath, cancellationToken);
            return Result.Success(command.DestinationFilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DatabaseAccessException)
        {
            // El detalle técnico lo registra quien invoca; al operador se le da un mensaje comprensible.
            return Result.Failure<string>(new ExportFailed(ExportFailedMessage));
        }
    }
}
