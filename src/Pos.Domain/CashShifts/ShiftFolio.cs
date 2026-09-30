using System.Globalization;

namespace Pos.Domain.CashShifts;

/// <summary>Folios legibles de turnos (<c>T-000123</c>) y de sus movimientos de efectivo (<c>T-000123-02</c>).</summary>
public static class ShiftFolio
{
    public static string Format(long number) =>
        string.Create(CultureInfo.InvariantCulture, $"T-{number:000000}");

    public static string FormatMovement(long number, int sequence) =>
        string.Create(CultureInfo.InvariantCulture, $"{Format(number)}-{sequence:00}");
}
