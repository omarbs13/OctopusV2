using Pos.Application.Abstractions;

namespace Pos.Application.Discounts;

/// <summary>La vigencia de los cupones se evalúa en la fecha local del equipo (spec, casos límite).</summary>
public static class DiscountDates
{
    public static DateOnly LocalToday(IClock clock, TimeZoneInfo? zone = null)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc),
            zone ?? TimeZoneInfo.Local));
    }
}
