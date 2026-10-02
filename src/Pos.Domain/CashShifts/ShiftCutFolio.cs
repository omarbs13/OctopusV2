using System.Globalization;

namespace Pos.Domain.CashShifts;

/// <summary>Folios legibles de cortes, consecutivos por tipo (<c>X-000001</c>, <c>Z-000001</c>).</summary>
public static class ShiftCutFolio
{
    public static string Format(ShiftCutType type, long number) =>
        string.Create(CultureInfo.InvariantCulture, $"{type.ToCode()}-{number:000000}");
}
