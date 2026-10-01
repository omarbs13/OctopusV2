using Pos.Domain.Common;

namespace Pos.Domain.Reports;

/// <summary>Motivo por el que un rango de fechas no es válido como período de reporte.</summary>
public enum ReportPeriodError
{
    EndBeforeStart,
    TooLong,
}

/// <summary>
/// Rango de fechas locales inclusivo de un reporte. La conversión a UTC la hace Application con la
/// zona horaria local (research §3).
/// </summary>
public sealed record ReportPeriod
{
    /// <summary>Máximo de días de un período (366, un año bisiesto).</summary>
    public const int MaxDays = 366;

    private ReportPeriod(DateOnly fromDate, DateOnly toDate, ReportPreset preset)
    {
        FromDate = fromDate;
        ToDate = toDate;
        Preset = preset;
    }

    public DateOnly FromDate { get; }

    public DateOnly ToDate { get; }

    public ReportPreset Preset { get; }

    /// <summary>Número de días del período, ambos extremos incluidos.</summary>
    public int Days => ToDate.DayNumber - FromDate.DayNumber + 1;

    /// <summary>Devuelve el motivo de invalidez del rango, o nulo si es válido.</summary>
    public static ReportPeriodError? Validate(DateOnly fromDate, DateOnly toDate)
    {
        if (toDate < fromDate)
        {
            return ReportPeriodError.EndBeforeStart;
        }

        return toDate.DayNumber - fromDate.DayNumber + 1 > MaxDays ? ReportPeriodError.TooLong : null;
    }

    public static ReportPeriod Custom(DateOnly fromDate, DateOnly toDate) => Create(fromDate, toDate, ReportPreset.Custom);

    public static ReportPeriod Today(DateOnly today) => Create(today, today, ReportPreset.Today);

    public static ReportPeriod Yesterday(DateOnly today) =>
        Create(today.AddDays(-1), today.AddDays(-1), ReportPreset.Yesterday);

    /// <summary>Los últimos 7 días, incluido hoy.</summary>
    public static ReportPeriod Last7Days(DateOnly today) => Create(today.AddDays(-6), today, ReportPreset.Last7Days);

    /// <summary>Del día 1 del mes actual a hoy.</summary>
    public static ReportPeriod ThisMonth(DateOnly today) =>
        Create(new DateOnly(today.Year, today.Month, 1), today, ReportPreset.ThisMonth);

    /// <summary>Todo el mes calendario anterior.</summary>
    public static ReportPeriod PreviousMonth(DateOnly today)
    {
        var firstOfThisMonth = new DateOnly(today.Year, today.Month, 1);
        return Create(firstOfThisMonth.AddMonths(-1), firstOfThisMonth.AddDays(-1), ReportPreset.PreviousMonth);
    }

    public static ReportPeriod FromPreset(ReportPreset preset, DateOnly today) => preset switch
    {
        ReportPreset.Today => Today(today),
        ReportPreset.Yesterday => Yesterday(today),
        ReportPreset.Last7Days => Last7Days(today),
        ReportPreset.ThisMonth => ThisMonth(today),
        ReportPreset.PreviousMonth => PreviousMonth(today),
        _ => throw new DomainException("El período personalizado requiere fechas."),
    };

    /// <summary>Rango de igual duración que termina el día anterior al inicio de este período.</summary>
    public ReportPeriod Previous() =>
        Create(FromDate.AddDays(-Days), FromDate.AddDays(-1), ReportPreset.Custom);

    private static ReportPeriod Create(DateOnly fromDate, DateOnly toDate, ReportPreset preset)
    {
        if (Validate(fromDate, toDate) is { } error)
        {
            throw new DomainException(error == ReportPeriodError.EndBeforeStart
                ? "La fecha final no puede ser anterior a la inicial."
                : $"El período no puede superar {MaxDays} días.");
        }

        return new ReportPeriod(fromDate, toDate, preset);
    }
}
