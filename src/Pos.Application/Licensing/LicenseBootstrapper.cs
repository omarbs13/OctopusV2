using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Domain.Licensing;

namespace Pos.Application.Licensing;

/// <summary>
/// Carga la licencia local al arrancar (011): crea el archivo en el primer arranque, regenera uno
/// borrado sin reiniciar el período y deja el estado en memoria. Nunca cierra la aplicación.
/// </summary>
public sealed partial class LicenseBootstrapper
{
    private readonly ILicenseStore _store;
    private readonly ILicenseState _state;
    private readonly IMachineIdProvider _machine;
    private readonly IInstallationAgeReader _age;
    private readonly IClock _clock;
    private readonly ILogger<LicenseBootstrapper> _logger;

    public LicenseBootstrapper(
        ILicenseStore store,
        ILicenseState state,
        IMachineIdProvider machine,
        IInstallationAgeReader age,
        IClock clock,
        ILogger<LicenseBootstrapper> logger)
    {
        _store = store;
        _state = state;
        _machine = machine;
        _age = age;
        _clock = clock;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            switch (_store.Load())
            {
                case LicenseLoadResult.Loaded loaded:
                    await ApplyLoadedAsync(loaded.Record);
                    break;
                case LicenseLoadResult.Missing:
                    await CreateAsync(cancellationToken);
                    break;
                case LicenseLoadResult.Invalid invalid:
                    _state.SetInvalid(invalid.Reason);
                    break;
            }
        }
#pragma warning disable CA1031 // Una falla de la licencia nunca debe cerrar la aplicación; queda en modo lectura.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogFailed(ex);
            _state.SetInvalid(InvalidLicenseReason.Corrupt);
        }
    }

    /// <summary>Avanza la última fecha vista (nunca retrocede) y la guarda solo si cambió.</summary>
    public Task TouchAsync()
    {
        if (_state.Record is { } record && _clock.UtcNow > record.LastSeenUtc)
        {
            SaveSafely(record with { LastSeenUtc = _clock.UtcNow });
        }

        return Task.CompletedTask;
    }

    private Task ApplyLoadedAsync(LicenseRecord record)
    {
        _state.Set(record);
        return TouchAsync();
    }

    private async Task CreateAsync(CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var firstUser = await _age.GetFirstUserCreatedUtcAsync(cancellationToken);
        var firstRun = firstUser is { } created && created < now ? created : now;
        var record = new LicenseRecord(1, _machine.GetMachineId(), firstRun, now, null);
        _state.Set(record);
        SaveSafely(record);
        LogCreated(firstRun);
    }

    private void SaveSafely(LicenseRecord record)
    {
        try
        {
            _store.Save(record);
            _state.Set(record);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // El estado en memoria sigue vigente; se reintenta en el próximo cambio de día.
            LogNotSaved(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Licencia local creada. Inicio del período: {FirstRunUtc:o}")]
    private partial void LogCreated(DateTime firstRunUtc);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se pudo guardar el archivo de licencia")]
    private partial void LogNotSaved(Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "No se pudo cargar la licencia; el sistema queda en modo lectura")]
    private partial void LogFailed(Exception ex);
}
