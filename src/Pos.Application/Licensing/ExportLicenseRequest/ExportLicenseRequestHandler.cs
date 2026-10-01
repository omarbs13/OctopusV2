using System.Globalization;
using System.Text.Json;
using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Licensing.ExportLicenseRequest;

/// <param name="DestinationFilePath">Archivo de solicitud que se creará.</param>
public sealed record ExportLicenseRequestCommand(string DestinationFilePath);

/// <summary>
/// Escribe el archivo de solicitud con el ID de máquina para enviarlo al proveedor (011, FR-011a). No
/// incluye datos del negocio ni de usuarios.
/// </summary>
public sealed class ExportLicenseRequestHandler
{
    public const string DestinationField = "DestinationFilePath";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly IAccessControl _access;
    private readonly IMachineIdProvider _machine;
    private readonly IAppInfo _appInfo;
    private readonly IClock _clock;

    public ExportLicenseRequestHandler(IAccessControl access, IMachineIdProvider machine, IAppInfo appInfo, IClock clock)
    {
        _access = access;
        _machine = machine;
        _appInfo = appInfo;
        _clock = clock;
    }

    public async Task<Result<string>> HandleAsync(ExportLicenseRequestCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var access = await _access.CheckAsync(Permission.ManageLicense, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<string>(access.Error!);
        }

        if (string.IsNullOrWhiteSpace(command.DestinationFilePath))
        {
            return Result.Failure<string>(new ValidationFailed([new FieldError(DestinationField, "Elija dónde guardar el archivo de solicitud.")]));
        }

        var request = new
        {
            format = 1,
            machineId = _machine.GetMachineId(),
            appVersion = _appInfo.Version,
            createdUtc = _clock.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
        };

        var temp = command.DestinationFilePath + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(request, JsonOptions), cancellationToken);
            File.Move(temp, command.DestinationFilePath, overwrite: true);
            return Result.Success(command.DestinationFilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDelete(temp);
            return Result.Failure<string>(new ExportFailed("No se pudo guardar el archivo de solicitud. Verifique que la ubicación tenga espacio y permisos de escritura."));
        }
    }

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Un temporal que no se pudo borrar no invalida el resultado.
        }
    }
}
