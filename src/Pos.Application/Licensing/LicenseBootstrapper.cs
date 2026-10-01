using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Domain.Licensing;

namespace Pos.Application.Licensing;

/// <summary>
/// Carga la licencia local al arrancar (012): concilia el archivo con la copia protegida, regenera el
/// archivo si falta o es inutilizable sin reiniciar la evaluación, migra el archivo 011 y deja el estado
/// en memoria. Nunca cierra la aplicación.
/// </summary>
public sealed partial class LicenseBootstrapper
{
    private static readonly IReadOnlySet<LicensedModule> NoModules = new HashSet<LicensedModule>();

    private readonly ILicenseStore _store;
    private readonly ILicenseSealStore _seals;
    private readonly ILicenseState _state;
    private readonly IMachineIdProvider _machine;
    private readonly IInstallationAgeReader _age;
    private readonly IServiceScopeFactory _scopes;
    private readonly IClock _clock;
    private readonly ILogger<LicenseBootstrapper> _logger;

    public LicenseBootstrapper(
        ILicenseStore store,
        ILicenseSealStore seals,
        ILicenseState state,
        IMachineIdProvider machine,
        IInstallationAgeReader age,
        IServiceScopeFactory scopes,
        IClock clock,
        ILogger<LicenseBootstrapper> logger)
    {
        _store = store;
        _seals = seals;
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
            var record = _store.Load() switch
            {
                LicenseLoadResult.Loaded loaded => Reconcile(loaded.Record, seal),
                LicenseLoadResult.LegacyV1 legacy => Migrate(legacy.License),
                LicenseLoadResult.Unusable => await RecoverAsync(seal, "Archivo inutilizable", cancellationToken),
                _ => await RecoverAsync(seal, "Archivo ausente", cancellationToken),
            };
            await PersistAsync(record, cancellationToken);
        }
#pragma warning disable CA1031 // Una falla de la licencia nunca debe cerrar la aplicación (Principio I).
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogFailed(ex);
            if (_state.Record is null)
            {
                _state.Set(NewRecord(_clock.UtcNow, NoModules));
            }
        }
    }

    /// <summary>Avanza la última fecha vista (nunca retrocede) y la guarda solo si cambió.</summary>
    public async Task TouchAsync()
    {
        if (_state.Record is { } record && _clock.UtcNow > record.LastSeenUtc)
        {
            await PersistAsync(record with { LastSeenUtc = _clock.UtcNow }, CancellationToken.None);
        }

        _state.Refresh();
    }

    private LicenseRecord Reconcile(LicenseRecord file, LicenseSeal? seal)
    {
        var firstRun = seal is not null && seal.FirstRunUtc < file.FirstRunUtc ? seal.FirstRunUtc : file.FirstRunUtc;
        var lastSeen = Max(file.LastSeenUtc, seal?.LastSeenUtc ?? file.LastSeenUtc, _clock.UtcNow);
        return file with { FirstRunUtc = firstRun, LastSeenUtc = lastSeen };
    }

    private async Task<LicenseRecord> RecoverAsync(LicenseSeal? seal, string reason, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        if (seal is null)
        {
            var firstUser = await _age.GetFirstUserCreatedUtcAsync(cancellationToken);
            var firstRun = firstUser is { } created && created < now ? created : now;
            LogCreated(firstRun);
            return NewRecord(firstRun, NoModules) with { LastSeenUtc = now };
        }

        FileWasRegenerated = true;
        LogRecovered(reason);
        await AuditRecoveredAsync(reason, cancellationToken);
        return NewRecord(seal.FirstRunUtc, NoModules) with { LastSeenUtc = Max(seal.LastSeenUtc, now, seal.FirstRunUtc) };
    }

    private LicenseRecord Migrate(LegacyLicense legacy)
    {
        var now = _clock.UtcNow;
        var lastSeen = Max(legacy.LastSeenUtc, now, legacy.FirstRunUtc);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(lastSeen, DateTimeKind.Utc), TimeZoneInfo.Local));
        var active = legacy.HasGrant && (legacy.ValidUntil is null || legacy.ValidUntil >= today);
        LogMigrated(active);
        return active
            ? NewRecord(legacy.FirstRunUtc, ModuleCatalog.All.ToHashSet()) with { LastSeenUtc = lastSeen }
            : NewRecord(now, NoModules);
    }

    private async Task PersistAsync(LicenseRecord record, CancellationToken cancellationToken)
    {
        _state.Set(record);
        try
        {
            _store.Save(record);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // El estado en memoria sigue vigente; se reintenta en el próximo cambio.
            LogNotSaved(ex);
        }

        try
        {
            await _seals.WriteAsync(new LicenseSeal(record.FirstRunUtc, record.LastSeenUtc), cancellationToken);
        }
#pragma warning disable CA1031 // La copia protegida es de mejor esfuerzo.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogNotSaved(ex);
        }
    }

    private async Task<LicenseSeal?> ReadSealSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _seals.ReadAsync(cancellationToken);
        }
#pragma warning disable CA1031 // Una copia ilegible equivale a una ausente.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogNotSaved(ex);
            return null;
        }
    }

    private async Task AuditRecoveredAsync(string reason, CancellationToken cancellationToken)
    {
        try
        {
            // La bitácora es de ámbito por operación; el arranque abre el suyo.
            using var scope = _scopes.CreateScope();
            var audit = scope.ServiceProvider.GetRequiredService<IAuditLog>();
            audit.Add(AuditActions.LicenseRecovered, AuditActions.LicenseEntity, Guid.CreateVersion7(), reason);
            await audit.SaveAsync(cancellationToken);
        }
#pragma warning disable CA1031 // La bitácora no debe impedir el arranque.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogNotSaved(ex);
        }
    }

    private LicenseRecord NewRecord(DateTime firstRun, IReadOnlySet<LicensedModule> modules) =>
        new(LicenseRecord.CurrentVersion, _machine.GetMachineId(), firstRun, firstRun, LicenseRecord.DefaultTrialDays, modules);

    private static DateTime Max(DateTime a, DateTime b, DateTime? c = null)
    {
        var max = a > b ? a : b;
        return c is { } third && third > max ? third : max;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Licencia local creada. Inicio de la evaluación: {FirstRunUtc:o}")]
    private partial void LogCreated(DateTime firstRunUtc);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Archivo de licencia regenerado desde la copia protegida: {Reason}")]
    private partial void LogRecovered(string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Licencia 011 migrada. Concesión activa: {Active}")]
    private partial void LogMigrated(bool active);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se pudo guardar la licencia o su copia protegida")]
    private partial void LogNotSaved(Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "No se pudo cargar la licencia; se continúa con una evaluación en memoria")]
    private partial void LogFailed(Exception ex);
}
