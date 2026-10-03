using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Licensing.GetLicenseStatus;
using Pos.Application.Users.Access;
using Pos.Domain.Licensing;
using Pos.Domain.Users;

namespace Pos.Application.Licensing.ImportLicense;

/// <param name="FilePath">Archivo <c>.lic</c> emitido por el proveedor.</param>
public sealed record ImportLicenseCommand(string FilePath);

/// <summary>
/// Importa una licencia formato 3 (025, FR-017 a FR-022): verifica en orden firma, máquina y antigüedad,
/// la guarda tal como llegó y la aplica al instante, sin reiniciar. Reemplaza a la anterior, no se suma
/// (FR-012). Un rechazo conserva la licencia vigente; importar y rechazar quedan en la bitácora.
/// </summary>
public sealed partial class ImportLicenseHandler
{
    private readonly IAccessControl _access;
    private readonly ILicenseVerifier _verifier;
    private readonly IInstalledLicenseStore _installed;
    private readonly ILicenseState _state;
    private readonly LicenseBootstrapper _bootstrapper;
    private readonly IMachineIdProvider _machine;
    private readonly IAuditLog _audit;
    private readonly IClock _clock;
    private readonly GetLicenseStatusHandler _status;
    private readonly ILogger<ImportLicenseHandler> _logger;

    public ImportLicenseHandler(
        IAccessControl access,
        ILicenseVerifier verifier,
        IInstalledLicenseStore installed,
        ILicenseState state,
        LicenseBootstrapper bootstrapper,
        IMachineIdProvider machine,
        IAuditLog audit,
        IClock clock,
        GetLicenseStatusHandler status,
        ILogger<ImportLicenseHandler> logger)
    {
        _access = access;
        _verifier = verifier;
        _installed = installed;
        _state = state;
        _bootstrapper = bootstrapper;
        _machine = machine;
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

        var content = await ReadAsync(command.FilePath, cancellationToken);
        var verification = content is null
            ? new LicenseVerification.Rejected(LicenseImportRejection.Unreadable)
            : _verifier.Verify(content, _machine.GetMachineId());

        // Paso 7 del contrato: misma licencia (reimportación) o emitida después que la vigente.
        if (verification is LicenseVerification.Valid candidate && !candidate.License.IsAcceptableReplacementFor(_state.License))
        {
            verification = new LicenseVerification.Rejected(LicenseImportRejection.NotNewer);
        }

        if (verification is LicenseVerification.Rejected rejected)
        {
            LogRejected(rejected.Reason);
            _audit.Add(AuditActions.LicenseRejectedOnImport, AuditActions.LicenseEntity, Guid.CreateVersion7(), rejected.Reason.ToString());
            await _audit.SaveAsync(cancellationToken);
            return Result.Failure<LicenseStatusDto>(new InvalidLicense(rejected.Reason));
        }

        var license = ((LicenseVerification.Valid)verification).License;
        var now = _clock.UtcNow;
        await _installed.ReplaceAsync(content!, now, cancellationToken);

        // Al aceptar la primera licencia, la prueba termina definitivamente (FR-026a).
        var trial = _state.Trial ?? new TrialRecord(_machine.GetMachineId(), now, now, TrialRecord.DefaultTrialDays, null);
        if (trial.LicenseImportedUtc is null)
        {
            trial = trial with { LicenseImportedUtc = now };
            await _bootstrapper.PersistAsync(trial, cancellationToken);
        }

        _state.Set(trial, license, false);
        _audit.Add(
            AuditActions.LicenseImported,
            AuditActions.LicenseEntity,
            license.LicenseId,
            $"{license.Grants.Count} entradas de módulo; emitida {license.IssuedAtUtc:yyyy-MM-dd HH:mm} UTC");
        await _audit.SaveAsync(cancellationToken);
        LogImported(license.LicenseId, license.Grants.Count);

        return Result.Success(_status.Handle());
    }

    /// <summary>Texto del archivo; nulo si no existe, excede 64 KiB o no se puede leer.</summary>
    private static async Task<string?> ReadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > ILicenseVerifier.MaxBytes)
            {
                return null;
            }

            return await File.ReadAllTextAsync(path, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Importación de licencia rechazada: {Reason}")]
    private partial void LogRejected(LicenseImportRejection reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Licencia importada. LicenseId={LicenseId} Entradas={Count}")]
    private partial void LogImported(Guid licenseId, int count);
}
