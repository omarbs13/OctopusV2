namespace Pos.Domain.Licensing;

/// <summary>
/// Reglas de la licencia (025, data-model y research §7–§8). Las fechas se comparan por fecha de
/// calendario local: el día 1 de prueba quedan 30, el día 30 queda 1 y el día 31 ya terminó.
/// </summary>
public static class LicenseEvaluator
{
    public const int NearDays = 5;

    public const int UrgentDays = 1;

    /// <summary>Días hacia adelante (incluido hoy) en los que se avisa de un vencimiento.</summary>
    public const int ExpiringSoonDays = 7;

    public static LicenseStatus Evaluate(
        TrialRecord trial,
        SignedLicense? license,
        bool storedLicenseRejected,
        DateTime nowUtc,
        TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(trial);
        ArgumentNullException.ThrowIfNull(zone);

        var today = ToLocalDate(nowUtc, zone);
        var lastSeen = ToLocalDate(trial.LastSeenUtc, zone);

        // Reloj atrasado: el estado no se reactiva (intersección) y el aviso solo sale con más de 1 día.
        var clockBehind = lastSeen.DayNumber - today.DayNumber > 1;

        if (license is not null)
        {
            var modules = ModuleCatalog.All.Select(m => StatusOf(m, license.Grants, today, lastSeen)).ToArray();
            var baseState = modules.First(s => s.Module == ModuleCatalog.Base).State;
            var expiring = modules
                .Where(s => s.State == ModuleState.Active && s.ExpiresOn is { } end
                    && end >= today && end.DayNumber <= today.DayNumber + ExpiringSoonDays)
                .ToArray();
            LicenseBlockReason? reason = baseState switch
            {
                ModuleState.Active => null,
                ModuleState.Pending => LicenseBlockReason.BasePending,
                ModuleState.Expired => LicenseBlockReason.BaseExpired,
                _ => LicenseBlockReason.BaseNotLicensed,
            };
            return new LicenseStatus(
                reason is null ? LicenseOverall.Licensed : LicenseOverall.Blocked,
                reason,
                0,
                LicenseWarning.None,
                license.CustomerName,
                modules,
                expiring,
                clockBehind,
                lastSeen,
                storedLicenseRejected);
        }

        // Tras haber licenciado, la prueba no vuelve aunque la licencia falte o no se verifique (FR-026a).
        if (storedLicenseRejected || trial.LicenseImportedUtc is not null)
        {
            return Blocked(LicenseBlockReason.LicenseInvalid, clockBehind, lastSeen, storedLicenseRejected);
        }

        if (trial.FirstRunUtc == DateTime.MinValue)
        {
            return Blocked(LicenseBlockReason.TrialExpired, clockBehind, lastSeen, storedLicenseRejected);
        }

        // Un reloj atrasado no devuelve días: se cuenta desde la fecha más reciente.
        var effective = today > lastSeen ? today : lastSeen;
        var remaining = trial.TrialDays - (effective.DayNumber - ToLocalDate(trial.FirstRunUtc, zone).DayNumber);
        if (remaining <= 0)
        {
            return Blocked(LicenseBlockReason.TrialExpired, clockBehind, lastSeen, storedLicenseRejected);
        }

        var warning = remaining switch
        {
            NearDays => LicenseWarning.Near,
            UrgentDays => LicenseWarning.Urgent,
            _ => LicenseWarning.None,
        };
        return new LicenseStatus(
            LicenseOverall.Trial,
            null,
            remaining,
            warning,
            null,
            ModuleCatalog.All.Select(m => new ModuleStatus(m, ModuleState.Active, null, null)).ToArray(),
            [],
            clockBehind,
            lastSeen,
            storedLicenseRejected);
    }

    /// <summary>
    /// Con el reloj atrasado (<paramref name="today"/> &lt; <paramref name="lastSeen"/>), un módulo solo está
    /// activo si lo está en ambas fechas: ningún atraso reactiva un módulo vencido (FR-038, FR-039).
    /// </summary>
    private static ModuleStatus StatusOf(LicensedModule module, IReadOnlyList<ModuleGrant> grants, DateOnly today, DateOnly lastSeen)
    {
        var entries = grants.Where(g => g.Module == module).ToArray();
        var status = StatusOn(module, entries, today);
        if (today < lastSeen && status.State == ModuleState.Active)
        {
            var seen = StatusOn(module, entries, lastSeen);
            if (seen.State != ModuleState.Active)
            {
                return seen;
            }
        }

        return status;
    }

    /// <summary>Varias entradas: Active si alguna; si no, Pending la más próxima; si no, Expired la más reciente.</summary>
    private static ModuleStatus StatusOn(LicensedModule module, ModuleGrant[] entries, DateOnly day)
    {
        if (entries.Length == 0)
        {
            return new ModuleStatus(module, ModuleState.NotLicensed, null, null);
        }

        var active = entries
            .Where(g => g.IsActiveOn(day))
            .OrderByDescending(g => g.ExpiresOn ?? DateOnly.MaxValue)
            .FirstOrDefault();
        if (active is not null)
        {
            return new ModuleStatus(module, ModuleState.Active, active.ActivatesOn, active.ExpiresOn);
        }

        var pending = entries
            .Where(g => !g.IsInvalid && g.ActivatesOn > day)
            .OrderBy(g => g.ActivatesOn)
            .FirstOrDefault();
        if (pending is not null)
        {
            return new ModuleStatus(module, ModuleState.Pending, pending.ActivatesOn, pending.ExpiresOn);
        }

        var expired = entries.OrderByDescending(g => g.ExpiresOn ?? DateOnly.MinValue).First();
        return new ModuleStatus(module, ModuleState.Expired, expired.ActivatesOn, expired.ExpiresOn);
    }

    private static LicenseStatus Blocked(LicenseBlockReason reason, bool clockBehind, DateOnly lastSeen, bool storedLicenseRejected) =>
        new(
            LicenseOverall.Blocked,
            reason,
            0,
            LicenseWarning.None,
            null,
            ModuleCatalog.All.Select(m => new ModuleStatus(m, ModuleState.NotLicensed, null, null)).ToArray(),
            [],
            clockBehind,
            lastSeen,
            storedLicenseRejected);

    private static DateOnly ToLocalDate(DateTime utc, TimeZoneInfo zone) =>
        utc == DateTime.MinValue
            ? DateOnly.MinValue
            : DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone));
}
