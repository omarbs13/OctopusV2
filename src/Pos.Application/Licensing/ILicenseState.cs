using Pos.Application.Abstractions;
using Pos.Domain.Licensing;

namespace Pos.Application.Licensing;

/// <summary>
/// Estado de la licencia en memoria. <see cref="Current"/> se recalcula con el reloj en cada lectura,
/// sin acceso a disco, así que cruzar la medianoche aplica el cambio sin reiniciar (012).
/// </summary>
public interface ILicenseState
{
    LicenseStatus Current { get; }

    /// <summary>Licencia local vigente; nula si aún no se cargó.</summary>
    LicenseRecord? Record { get; }

    /// <summary>Módulos que hoy están activos (todos durante la evaluación).</summary>
    IReadOnlyCollection<LicensedModule> EnabledModules { get; }

    /// <summary>Se dispara al reemplazar el registro (importar) o al cambiar de fase.</summary>
    event EventHandler? Changed;

    bool IsModuleActive(LicensedModule module);

    void Set(LicenseRecord record);

    /// <summary>Vuelve a evaluar con el reloj y dispara <see cref="Changed"/> si cambió la fase.</summary>
    void Refresh();
}

public sealed class LicenseState : ILicenseState
{
    private readonly IClock _clock;
    private readonly Lock _gate = new();
    private LicenseRecord? _record;
    private LicensePhase? _lastPhase;

    public LicenseState(IClock clock) => _clock = clock;

    public event EventHandler? Changed;

    public LicenseRecord? Record
    {
        get
        {
            lock (_gate)
            {
                return _record;
            }
        }
    }

    /// <summary>Antes de cargar la licencia (solo ocurre en pruebas) no se restringe nada.</summary>
    public LicenseStatus Current
    {
        get
        {
            var record = Record;
            return record is null
                ? new LicenseStatus(LicensePhase.Trial, LicenseRecord.DefaultTrialDays, LicenseWarning.None, new HashSet<LicensedModule>())
                : LicenseEvaluator.Evaluate(record, _clock.UtcNow, TimeZoneInfo.Local);
        }
    }

    public IReadOnlyCollection<LicensedModule> EnabledModules =>
        ModuleCatalog.All.Where(IsModuleActive).ToArray();

    public bool IsModuleActive(LicensedModule module) => Current.IsModuleActive(module);

    public void Set(LicenseRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        lock (_gate)
        {
            _record = record;
        }

        _lastPhase = Current.Phase;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Refresh()
    {
        var phase = Current.Phase;
        if (_lastPhase is { } last && last == phase)
        {
            return;
        }

        _lastPhase = phase;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
