using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Licensing.GetLicenseStatus;
using Pos.Application.Users.Access;
using Pos.Domain.Licensing;
using Pos.Domain.Users;

namespace Pos.Application.Licensing.ImportLicense;

/// <param name="FilePath">Archivo de licencia emitido por el proveedor.</param>
public sealed record ImportLicenseCommand(string FilePath);

/// <summary>
/// Importa una licencia firmada y la aplica de inmediato, sin reiniciar (011, FR-011, FR-012). Un
/// rechazo conserva la licencia vigente; importar queda en la bitácora sin el contenido del archivo.
/// </summary>
public sealed partial class ImportLicenseHandler
{
    private readonly IAccessControl _access;
    private readonly ILicenseVerifier _verifier;
    private readonly ILicenseStore _store;
    private readonly ILicenseState _state;
    private readonly IMachineIdProvider _machine;
    private readonly IInstallationAgeReader _age;
    private readonly IAuditLog _audit;
    private readonly IClock _clock;
    private readonly GetLicenseStatusHandler _status;
    private readonly ILogger<ImportLicenseHandler> _logger;

    public ImportLicenseHandler(
        IAccessControl access,
        ILicenseVerifier verifier,
        ILicenseStore store,
        ILicenseState state,
        IMachineIdProvider machine,
        IInstallationAgeReader age,
        IAuditLog audit,
        IClock clock,
        GetLicenseStatusHandler status,
        ILogger<ImportLicenseHandler> logger)
    {
        _access = access;
        _verifier = verifier;
        _store = store;
        _state = state;
        _machine = machine;
        _age = age;
        _audit = audit;
        _clock = clock;
        _status = status;
        _logger = logger;
    }

    public async Task<Result<LicenseStatusDto>> HandleAsync(ImportLicenseCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var access = await _access.CheckAsync(Permission.ManageLicense, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<LicenseStatusDto>(access.Error!);
        }

        var verification = _verifier.Verify(command.FilePath, _machine.GetMachineId());
        if (verification is LicenseVerification.Rejected rejected)
        {
            LogRejected(rejected.Reason);
            return Result.Failure<LicenseStatusDto>(new InvalidLicense(rejected.Reason));
        }

        var grant = ((LicenseVerification.Valid)verification).Grant;
        var current = _state.Record;
        if (current?.Grant is { } active && grant.IssuedAtUtc < active.IssuedAtUtc)
        {
            LogRejected(LicenseImportRejection.Older);
            return Result.Failure<LicenseStatusDto>(new InvalidLicense(LicenseImportRejection.Older));
        }

        var now = _clock.UtcNow;
        var firstRun = current?.FirstRunUtc ?? await EarliestEvidenceAsync(now, cancellationToken);
        var lastSeen = current is not null && current.LastSeenUtc > now ? current.LastSeenUtc : now;
        var record = new LicenseRecord(1, _machine.GetMachineId(), firstRun, lastSeen, grant);

        try
        {
            _store.Save(record);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogNotSaved(ex);
            return Result.Failure<LicenseStatusDto>(new InvalidLicense(LicenseImportRejection.Unreadable));
        }

        _state.Set(record);
        _audit.Add(
            AuditActions.LicenseImported,
            AuditActions.LicenseEntity,
            Guid.CreateVersion7(),
            grant.ValidUntil is { } until ? $"Vigente hasta {until:yyyy-MM-dd}" : "Sin vencimiento");
        await _audit.SaveAsync(cancellationToken);
        LogImported(grant.ValidUntil);

        return Result.Success(_status.Handle());
    }

    private async Task<DateTime> EarliestEvidenceAsync(DateTime now, CancellationToken cancellationToken)
    {
        var firstUser = await _age.GetFirstUserCreatedUtcAsync(cancellationToken);
        return firstUser is { } created && created < now ? created : now;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Importación de licencia rechazada: {Reason}")]
    private partial void LogRejected(LicenseImportRejection reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se pudo guardar el archivo de licencia importado")]
    private partial void LogNotSaved(Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Licencia importada. Vigente hasta {ValidUntil}")]
    private partial void LogImported(DateOnly? validUntil);
}
