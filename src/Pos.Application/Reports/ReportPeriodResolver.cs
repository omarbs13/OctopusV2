using Pos.Application.Sales;
using Pos.Domain.Reports;

namespace Pos.Application.Reports;

/// <summary>
/// Convierte períodos de fechas locales a ventanas UTC con la zona horaria indicada. Es el único lugar
/// que conoce la zona local, para poder probarlo con una zona fija (contracts/application-ports.md).
/// </summary>
public sealed class ReportPeriodResolver
{
    private readonly TimeZoneInfo _zone;

    public ReportPeriodResolver(TimeZoneInfo zone) => _zone = zone;

    /// <summary>Resolutor con la zona horaria del equipo.</summary>
    public static ReportPeriodResolver ForLocalZone() => new(TimeZoneInfo.Local);

    /// <summary>Del inicio del primer día local al inicio del día siguiente al último, en UTC.</summary>
    public ReportWindow Resolve(ReportPeriod period)
    {
        ArgumentNullException.ThrowIfNull(period);
        return new ReportWindow(period, StartOfDayUtc(period.FromDate), StartOfDayUtc(period.ToDate.AddDays(1)));
    }

    /// <summary>Un <see cref="DayWindow"/> por cada día local del período, incluidos los días sin datos.</summary>
    public IReadOnlyList<DayWindow> Days(ReportPeriod period)
    {
        ArgumentNullException.ThrowIfNull(period);
        return [.. Enumerable.Range(0, period.Days).Select(i =>
        {
            var date = period.FromDate.AddDays(i);
            return new DayWindow(date, StartOfDayUtc(date), StartOfDayUtc(date.AddDays(1)));
        })];
    }

    /// <summary>Inicio (00:00 local) del día, en UTC; si la medianoche no existe por el cambio de horario, la primera hora válida.</summary>
    public DateTime StartOfDayUtc(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        while (_zone.IsInvalidTime(local))
        {
            local = local.AddMinutes(30);
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, _zone);
    }

    /// <summary>Fecha local de un instante UTC.</summary>
    public DateOnly ToLocalDate(DateTime utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _zone));

    /// <summary>Hora local de un instante UTC.</summary>
    public DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _zone);
}
