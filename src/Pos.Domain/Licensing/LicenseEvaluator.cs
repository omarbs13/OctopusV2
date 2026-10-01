namespace Pos.Domain.Licensing;

/// <summary>
/// Reglas de la licencia (011, research §4). Los días se cuentan por fecha de calendario local y
/// con 0 días restantes el sistema ya está vencido.
/// </summary>
public static class LicenseEvaluator
{
    public const int TrialDays = 30;

    public const int NearDays = 5;

    public const int UrgentDays = 1;

    public static LicenseStatus Evaluate(LicenseRecord record, DateTime nowUtc, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(zone);

        // Un reloj atrasado no devuelve días: la fecha efectiva nunca es anterior a la última vista.
        var effective = nowUtc > record.LastSeenUtc ? nowUtc : record.LastSeenUtc;
        var today = ToLocalDate(effective, zone);

        if (record.Grant is { ValidUntil: null })
        {
            return new LicenseStatus(LicenseKind.Licensed, null, false, LicenseWarning.None);
        }

        var remaining = record.Grant is { ValidUntil: { } until }
            ? until.DayNumber - today.DayNumber
            : TrialDays - (today.DayNumber - ToLocalDate(record.FirstRunUtc, zone).DayNumber);

        if (remaining <= 0)
        {
            return new LicenseStatus(LicenseKind.Expired, 0, true, LicenseWarning.None);
        }

        var warning = remaining <= UrgentDays
            ? LicenseWarning.Urgent
            : remaining <= NearDays ? LicenseWarning.Near : LicenseWarning.None;
        return new LicenseStatus(
            record.Grant is null ? LicenseKind.Trial : LicenseKind.Licensed,
            remaining,
            false,
            warning);
    }

    private static DateOnly ToLocalDate(DateTime utc, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone));
}
