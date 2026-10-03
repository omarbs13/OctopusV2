using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Domain.Licensing;

namespace Pos.Application.Licensing;

/// <summary>
/// Carga la licencia al arrancar (025): concilia el archivo de prueba con la copia protegida, regenera el
/// archivo si falta o es inutilizable sin reiniciar la prueba, reverifica la licencia guardada (pasos 1 a
/// 6 del contrato) y deja el estado en memoria. Nunca cierra la aplicación (Principio I).
/// </summary>
public sealed partial class LicenseBootstrapper
{
    private readonly ILicenseStore _store;
    private readonly ILicenseSealStore _seals;
    private readonly IInstalledLicenseStore _installed;
    private readonly ILicenseVerifier _verifier;
    private readonly ILicenseState _state;
    private readonly IMachineIdProvider _machine;
    private readonly IInstallationAgeReader _age;
    private readonly IServiceScopeFactory _scopes;
    private readonly IClock _clock;
    private readonly ILogger<LicenseBootstrapper> _logger;

    public LicenseBootstrapper(
        ILicenseStore store,
        ILicenseSealStore seals,
        IInstalledLicenseStore installed,
        ILicenseVerifier verifier,
        ILicenseState state,
        IMachineIdProvider machine,
        IInstallationAgeReader age,
        IServiceScopeFactory scopes,
        IClock clock,
        ILogger<LicenseBootstrapper> logger)
    {
        _store = store;
        _seals = seals;
        _installed = installed;
        _verifier = verifier;
        _state = state;
        _machine = machine;
        _age = age;
        _scopes = scopes;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Verdadero si este arranque regeneró el archivo desde la copia protegida (para avisar al operador).</summary>
    public bool FileWasRegenerated { get; private set; }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            var seal = await ReadSealSafelyAsync(cancellationToken);
            var load = _store.Load();
            var trial = load switch
            {
                LicenseLoadResult.Loaded loaded => Reconcile(loaded.Record, seal),
                LicenseLoadResult.Unusable => await RecoverAsync(seal, "Archivo inutilizable", cancellationToken),
                _ => await RecoverAsync(seal, "Archivo ausente", cancellationToken),
            };

            var storedRejected = false;
            if (load is LicenseLoadResult.Loaded { HadLegacyModules: true })
            {
                // Licencia de 011/012 que ya no se reconoce (FR-021): se trata como licencia guardada inválida.
                storedRejected = true;
                trial = trial with { LicenseImportedUtc = trial.LicenseImportedUtc ?? _clock.UtcNow };
                LogStoredRejected("licencia anterior al formato 3");
                await AuditAsync(AuditActions.LicenseStoredRejected, "Licencia anterior al formato 3", cancellationToken);
            }

            var license = await LoadInstalledAsync(cancellationToken);
            if (license is LicenseVerification.Valid valid)
            {
                // Un corte entre guardar la licencia y el sello pudo dejar la marca sin fijar (FR-026a).
                trial = trial with { LicenseImportedUtc = trial.LicenseImportedUtc ?? _clock.UtcNow };
            }
            else if (license is LicenseVerification.Rejected rejected)
            {
                storedRejected = true;
                LogStoredRejected(rejected.Reason.ToString());
                await AuditAsync(AuditActions.LicenseStoredRejected, rejected.Reason.ToString(), cancellationToken);
            }

            await PersistAsync(trial, cancellationToken);
            _state.Set(trial, (license as LicenseVerification.Valid)?.License, storedRejected);
        }
#pragma warning disable CA1031 // Una falla de la licencia nunca debe cerrar la aplicación (Principio I).
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogFailed(ex);
            if (_state.Trial is null)
            {
                _state.Set(NewTrial(_clock.UtcNow), null, false);
            }
        }
    }

    /// <summary>
    /// Avanza la última fecha vista (nunca retrocede), la guarda si cambió y reevalúa el estado (025, FR-034).
    /// Lo llaman el temporizador de medianoche y el inicio de sesión.
    /// </summary>
    public async Task TouchAsync()
    {
        try
        {
            if (_state.Trial is { } trial && _clock.UtcNow > trial.LastSeenUtc)
            {
                var updated = trial with { LastSeenUtc = _clock.UtcNow };
                await PersistAsync(updated, CancellationToken.None);
                _state.Set(updated, _state.License, _state.Current.StoredLicenseRejected);
                return;
            }

            _state.Refresh();
        }
#pragma warning disable CA1031 // Una falla de la licencia nunca debe cerrar la aplicación (Principio I).
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogFailed(ex);
        }
    }

    /// <summary>Guarda el registro en el archivo y en la copia protegida; ambos son de mejor esfuerzo.</summary>
    internal async Task PersistAsync(TrialRecord trial, CancellationToken cancellationToken)
    {
        try
        {
            _store.Save(trial);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // El estado en memoria sigue vigente; se reintenta en el próximo cambio.
            LogNotSaved(ex);
        }

        try
        {
            await _seals.WriteAsync(new LicenseSeal(trial.FirstRunUtc, trial.LastSeenUtc, trial.LicenseImportedUtc), cancellationToken);
        }
#pragma warning disable CA1031 // La copia protegida es de mejor esfuerzo.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogNotSaved(ex);
        }
    }

    /// <summary>
    /// <see cref="TrialRecord.FirstRunUtc"/> nunca aumenta, <see cref="TrialRecord.LastSeenUtc"/> nunca retrocede
    /// y <see cref="TrialRecord.LicenseImportedUtc"/> nunca vuelve a nulo. Un sello alterado vence la prueba (FR-025).
    /// </summary>
    private TrialRecord Reconcile(TrialRecord file, LicenseSealReadResult seal)
    {
        var now = _clock.UtcNow;
        var record = file with { MachineId = _machine.GetMachineId() };
        return seal switch
        {
            LicenseSealReadResult.Valid { Seal: var s } => record with
            {
                FirstRunUtc = s.FirstRunUtc < file.FirstRunUtc ? s.FirstRunUtc : file.FirstRunUtc,
                LastSeenUtc = Max(file.LastSeenUtc, s.LastSeenUtc, now),
                LicenseImportedUtc = file.LicenseImportedUtc ?? s.LicenseImportedUtc,
            },
            LicenseSealReadResult.Tampered => Expired(record with { LastSeenUtc = Max(file.LastSeenUtc, now) }),
            _ => record with { LastSeenUtc = Max(file.LastSeenUtc, now) },
        };
    }

    private async Task<TrialRecord> RecoverAsync(LicenseSealReadResult seal, string reason, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        switch (seal)
        {
            case LicenseSealReadResult.Valid { Seal: var s }:
                FileWasRegenerated = true;
                LogRecovered(reason);
                await AuditAsync(AuditActions.LicenseRecovered, reason, cancellationToken);
                return NewTrial(s.FirstRunUtc) with
                {
                    LastSeenUtc = Max(s.LastSeenUtc, now, s.FirstRunUtc),
                    LicenseImportedUtc = s.LicenseImportedUtc,
                };

            case LicenseSealReadResult.Tampered:
                LogRecovered(reason + "; copia protegida alterada");
                return Expired(NewTrial(now));

            default:
                var firstUser = await _age.GetFirstUserCreatedUtcAsync(cancellationToken);
                var firstRun = firstUser is { } created && created < now ? created : now;
                LogCreated(firstRun);
                return NewTrial(firstRun) with { LastSeenUtc = now };
        }
    }

    private async Task<LicenseVerification?> LoadInstalledAsync(CancellationToken cancellationToken)
    {
        var content = await _installed.ReadAsync(cancellationToken);
        return content is null ? null : _verifier.Verify(content, _machine.GetMachineId());
    }

    private async Task<LicenseSealReadResult> ReadSealSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _seals.ReadAsync(cancellationToken);
        }
#pragma warning disable CA1031 // Una falla de E/S al leer la copia equivale a una copia ausente, no alterada.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogNotSaved(ex);
            return new LicenseSealReadResult.Missing();
        }
    }

    private async Task AuditAsync(string action, string detail, CancellationToken cancellationToken)
    {
        try
        {
            // La bitácora es de ámbito por operación; el arranque abre el suyo.
            using var scope = _scopes.CreateScope();
            var audit = scope.ServiceProvider.GetRequiredService<IAuditLog>();
            audit.Add(action, AuditActions.LicenseEntity, Guid.CreateVersion7(), detail);
            await audit.SaveAsync(cancellationToken);
        }
#pragma warning disable CA1031 // La bitácora no debe impedir el arranque.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogNotSaved(ex);
        }
    }

    private TrialRecord NewTrial(DateTime firstRun) =>
        new(_machine.GetMachineId(), firstRun, firstRun, TrialRecord.DefaultTrialDays, null);

    private static TrialRecord Expired(TrialRecord record) => record with { FirstRunUtc = DateTime.MinValue };

    private static DateTime Max(DateTime a, DateTime b, DateTime? c = null)
    {
        var max = a > b ? a : b;
        return c is { } third && third > max ? third : max;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Registro de prueba creado. Inicio de la prueba: {FirstRunUtc:o}")]
    private partial void LogCreated(DateTime firstRunUtc);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Archivo de prueba regenerado: {Reason}")]
    private partial void LogRecovered(string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "La licencia guardada no superó la verificación al arrancar: {Reason}")]
    private partial void LogStoredRejected(string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se pudo guardar el registro de prueba o su copia protegida")]
    private partial void LogNotSaved(Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "No se pudo cargar la licencia; se continúa con una prueba en memoria")]
    private partial void LogFailed(Exception ex);
}
