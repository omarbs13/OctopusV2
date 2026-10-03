using System.Globalization;
using Pos.Application.Abstractions;
using Pos.Domain.Licensing;

namespace Pos.Application.Licensing;

/// <summary>
/// Estado de la licencia en memoria (025). <see cref="Current"/> se evalúa con el reloj sin acceso a
/// disco, así que cruzar la medianoche aplica el cambio en los casos de uso sin reiniciar.
/// </summary>
public interface ILicenseState
{
    LicenseStatus Current { get; }

    /// <summary>Registro de la prueba; nulo si aún no se cargó.</summary>
    TrialRecord? Trial { get; }

    /// <summary>Licencia firmada vigente (ya verificada); nula si no hay.</summary>
    SignedLicense? License { get; }

    /// <summary>Se dispara al reemplazar el estado (arranque, importar) o cuando la evaluación cambia.</summary>
    event EventHandler? Changed;

    bool IsModuleActive(LicensedModule module);

    void Set(TrialRecord trial, SignedLicense? license, bool storedLicenseRejected);

    /// <summary>Vuelve a evaluar con el reloj y dispara <see cref="Changed"/> solo si cambió la huella del estado.</summary>
    void Refresh();
}

public sealed class LicenseState : ILicenseState
{
    private readonly IClock _clock;
    private readonly Lock _gate = new();
    private TrialRecord? _trial;
    private SignedLicense? _license;
    private bool _storedRejected;
    private int _version;
    private (DateOnly Day, int Version, LicenseStatus Status)? _cache;
    private string? _lastFingerprint;

    public LicenseState(IClock clock) => _clock = clock;

    public event EventHandler? Changed;

    public TrialRecord? Trial
    {
        get
        {
            lock (_gate)
            {
                return _trial;
            }
        }
    }

    public SignedLicense? License
    {
        get
        {
            lock (_gate)
            {
                return _license;
            }
        }
    }

    /// <summary>Antes de cargar la licencia (solo ocurre en pruebas) no se restringe nada.</summary>
    public LicenseStatus Current
    {
        get
        {
            var now = _clock.UtcNow;
            var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(now, DateTimeKind.Utc), TimeZoneInfo.Local));
            lock (_gate)
            {
                if (_trial is null)
                {
                    return Unrestricted(day);
                }

                // Se lee en cada verificación de permiso: se cachea por fecha local y por registro.
                if (_cache is { } cached && cached.Day == day && cached.Version == _version)
                {
                    return cached.Status;
                }

                var status = LicenseEvaluator.Evaluate(_trial, _license, _storedRejected, now, TimeZoneInfo.Local);
                _cache = (day, _version, status);
                return status;
            }
        }
    }

    public bool IsModuleActive(LicensedModule module) => Current.IsModuleActive(module);

    public void Set(TrialRecord trial, SignedLicense? license, bool storedLicenseRejected)
    {
        ArgumentNullException.ThrowIfNull(trial);
        lock (_gate)
        {
            _trial = trial;
            _license = license;
            _storedRejected = storedLicenseRejected;
            _version++;
        }

        _lastFingerprint = Fingerprint(Current);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Refresh()
    {
        var fingerprint = Fingerprint(Current);
        if (fingerprint == _lastFingerprint)
        {
            return;
        }

        _lastFingerprint = fingerprint;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Estado general, estado de cada módulo y avisos (research §9).</summary>
    private static string Fingerprint(LicenseStatus status) => string.Join(
        '|',
        status.Overall,
        status.BlockReason,
        status.TrialDaysRemaining.ToString(CultureInfo.InvariantCulture),
        status.TrialWarning,
        status.ClockBehind,
        status.StoredLicenseRejected,
        string.Join(',', status.Modules.Select(m => m.State)),
        string.Join(',', status.ExpiringSoon.Select(m => m.Module)));

    private static LicenseStatus Unrestricted(DateOnly day) => new(
        LicenseOverall.Trial,
        null,
        TrialRecord.DefaultTrialDays,
        LicenseWarning.None,
        null,
        ModuleCatalog.All.Select(m => new ModuleStatus(m, ModuleState.Active, null, null)).ToArray(),
        [],
        false,
        day,
        false);
}
