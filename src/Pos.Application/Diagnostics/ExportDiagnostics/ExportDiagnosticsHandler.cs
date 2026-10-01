using Pos.Application.Abstractions;
using Pos.Application.Startup;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Diagnostics.ExportDiagnostics;

public sealed class ExportDiagnosticsHandler
{
    public const string DestinationField = "DestinationFilePath";
    public const string DestinationRequiredMessage = "Elija dónde guardar el archivo de diagnóstico.";
    public const string ExportFailedMessage =
        "No se pudo exportar el diagnóstico. Verifique que la ubicación elegida tenga espacio y permisos de escritura.";

    private readonly IAccessControl _access;
    private readonly IDiagnosticsExporter _exporter;

    public ExportDiagnosticsHandler(IAccessControl access, IDiagnosticsExporter exporter)
    {
        _access = access;
        _exporter = exporter;
    }

    public async Task<Result<ExportDiagnosticsResult>> HandleAsync(ExportDiagnosticsCommand command, CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(Permission.ExportDiagnostics, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<ExportDiagnosticsResult>(access.Error!);
        }

        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.DestinationFilePath))
        {
            return Result.Failure<ExportDiagnosticsResult>(new ValidationFailed([new FieldError(DestinationField, DestinationRequiredMessage)]));
        }

        try
        {
            var logFiles = await _exporter.ExportAsync(command.DestinationFilePath, command.IncludeDatabase, cancellationToken);
            return Result.Success(new ExportDiagnosticsResult(command.DestinationFilePath, logFiles));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DatabaseAccessException)
        {
            // El detalle técnico lo registra quien invoca; al operador se le da un mensaje comprensible.
            return Result.Failure<ExportDiagnosticsResult>(new ExportFailed(ExportFailedMessage));
        }
    }
}
