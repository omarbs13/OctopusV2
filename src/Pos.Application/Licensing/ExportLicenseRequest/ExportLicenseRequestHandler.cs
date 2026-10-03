using System.Globalization;
using System.Text.Json;
using Pos.Application.Abstractions;
using Pos.Application.Business;
using Pos.Application.Users.Access;

namespace Pos.Application.Licensing.ExportLicenseRequest;

/// <param name="DestinationFilePath">Archivo de solicitud que se creará.</param>
public sealed record ExportLicenseRequestCommand(string DestinationFilePath);

/// <summary>
/// Escribe la solicitud <c>.octoreq</c> para el proveedor (025, FR-013 a FR-016, contracts/license-request.md).
/// No es secreta ni va cifrada ni firmada (FR-014), así que basta con una sesión: cualquier usuario la genera,
/// también en bloqueo. No incluye datos de usuarios.
/// </summary>
public sealed class ExportLicenseRequestHandler
{
    public const string DestinationField = "DestinationFilePath";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly IAccessControl _access;
    private readonly IMachineIdProvider _machine;
    private readonly IAppInfo _appInfo;
    private readonly IClock _clock;
    private readonly IBusinessProfileRepository _business;
    private readonly IModuleCatalogInfo _catalog;

    public ExportLicenseRequestHandler(
        IAccessControl access,
        IMachineIdProvider machine,
        IAppInfo appInfo,
        IClock clock,
        IBusinessProfileRepository business,
        IModuleCatalogInfo catalog)
    {
        _access = access;
        _machine = machine;
        _appInfo = appInfo;
        _clock = clock;
        _business = business;
        _catalog = catalog;
    }

    public async Task<Result<string>> HandleAsync(ExportLicenseRequestCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var access = await _access.CheckSessionAsync(cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<string>(access.Error!);
        }

        if (string.IsNullOrWhiteSpace(command.DestinationFilePath))
        {
            return Result.Failure<string>(new ValidationFailed([new FieldError(DestinationField, "Elija dónde guardar el archivo de solicitud.")]));
        }

        var profile = await _business.GetAsync(cancellationToken);
        var request = new
        {
            requestFormat = 2,
            machineId = _machine.GetMachineId(),
            businessName = profile?.TradeName ?? string.Empty,
            appVersion = _appInfo.Version,
            catalogVersion = _catalog.CatalogVersion,
            createdAtUtc = _clock.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
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
