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
/// Importa una licencia extendida y suma sus módulos de inmediato, sin reiniciar (012, FR-010, FR-011, FR-017). Un
/// rechazo conserva la licencia vigente; importar queda en la bitácora sin el contenido del archivo.
/// </summary>
public sealed partial class ImportLicenseHandler
{
    private readonly IAccessControl _access;
    private readonly ILicenseVerifier _verifier;
    private readonly ILicenseStore _store;
    private readonly ILicenseSealStore _seals;
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
        ILicenseSealStore seals,
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
        _seals = seals;
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
        var now = _clock.UtcNow;
        var firstRun = current?.FirstRunUtc ?? await EarliestEvidenceAsync(now, cancellationToken);
        var lastSeen = current is not null && current.LastSeenUtc > now ? current.LastSeenUtc : now;
        var modules = new HashSet<LicensedModule>(current?.Modules ?? new HashSet<LicensedModule>());
        modules.UnionWith(grant.Modules);
        var record = new LicenseRecord(
            LicenseRecord.CurrentVersion,
            _machine.GetMachineId(),
            firstRun,
            lastSeen,
            current?.TrialDays ?? LicenseRecord.DefaultTrialDays,
            modules);

        try
        {
            _store.Save(record);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogNotSaved(ex);
            return Result.Failure<LicenseStatusDto>(new InvalidLicense(LicenseImportRejection.Unreadable));
        }

        await _seals.WriteAsync(new LicenseSeal(record.FirstRunUtc, record.LastSeenUtc), cancellationToken);
        _state.Set(record);
        _audit.Add(
            AuditActions.LicenseImported,
            AuditActions.LicenseEntity,
            Guid.CreateVersion7(),
            $"{grant.Modules.Count} módulos activados");
        await _audit.SaveAsync(cancellationToken);
        LogImported(grant.Modules.Count);

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

    [LoggerMessage(Level = LogLevel.Information, Message = "Licencia importada. Módulos activados: {Count}")]
    private partial void LogImported(int count);
}
