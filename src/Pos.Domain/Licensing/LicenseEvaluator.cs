namespace Pos.Domain.Licensing;

/// <summary>
/// Reglas de la evaluación (012). Los días se cuentan por fecha de calendario local: el día 1 quedan 30,
/// el día 30 queda 1 y el día 31 ya es modular.
/// </summary>
public static class LicenseEvaluator
{
    public const int NearDays = 5;

    public const int UrgentDays = 1;

    public static LicenseStatus Evaluate(LicenseRecord record, DateTime nowUtc, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(zone);

        // Un reloj atrasado no devuelve días: la fecha efectiva nunca es anterior a la última vista.
        var effective = nowUtc > record.LastSeenUtc ? nowUtc : record.LastSeenUtc;
        var today = ToLocalDate(effective, zone);
        var remaining = record.TrialDays - (today.DayNumber - ToLocalDate(record.FirstRunUtc, zone).DayNumber);

        if (remaining <= 0)
        {
            return new LicenseStatus(LicensePhase.Modular, 0, LicenseWarning.None, record.Modules);
        }

        var warning = remaining switch
        {
            NearDays => LicenseWarning.Near,
            UrgentDays => LicenseWarning.Urgent,
            _ => LicenseWarning.None,
        };
        return new LicenseStatus(LicensePhase.Trial, remaining, warning, record.Modules);
    }

    private static DateOnly ToLocalDate(DateTime utc, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone));
}
