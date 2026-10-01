using Pos.Application.Abstractions;
using Pos.Domain.Licensing;
using Pos.Domain.Users;

namespace Pos.Application.Licensing;

/// <summary>
/// Estado de la licencia en memoria. <see cref="Current"/> se recalcula con el reloj en cada lectura,
/// sin acceso a disco, así que cruzar la medianoche bloquea sin reiniciar (011, FR-007).
/// </summary>
public interface ILicenseState
{
    LicenseStatus Current { get; }

    /// <summary>Licencia local vigente; nula si el archivo es inválido o aún no se cargó.</summary>
    LicenseRecord? Record { get; }

    void Set(LicenseRecord record);

    void SetInvalid(InvalidLicenseReason reason);

    /// <summary>En modo lectura se bloquean la venta, los reportes y la administración de usuarios.</summary>
    bool IsBlocked(Permission permission);
}

public sealed class LicenseState : ILicenseState
{
    private static readonly LicenseStatus Unrestricted = new(LicenseKind.Trial, LicenseEvaluator.TrialDays, false, LicenseWarning.None);

    private readonly IClock _clock;
    private readonly Lock _gate = new();
    private LicenseRecord? _record;
    private InvalidLicenseReason? _invalid;

    public LicenseState(IClock clock) => _clock = clock;

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
            LicenseRecord? record;
            InvalidLicenseReason? invalid;
            lock (_gate)
            {
                (record, invalid) = (_record, _invalid);
            }

            if (invalid is { } reason)
            {
                return LicenseStatus.Invalid(reason);
            }

            return record is null ? Unrestricted : LicenseEvaluator.Evaluate(record, _clock.UtcNow, TimeZoneInfo.Local);
        }
    }

    public void Set(LicenseRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        lock (_gate)
        {
            (_record, _invalid) = (record, null);
        }
    }

    public void SetInvalid(InvalidLicenseReason reason)
    {
        lock (_gate)
        {
            (_record, _invalid) = (null, reason);
        }
    }

    public bool IsBlocked(Permission permission) =>
        permission is Permission.Sell or Permission.ViewReports or Permission.ManageUsers
        && Current.IsReadOnly;
}
