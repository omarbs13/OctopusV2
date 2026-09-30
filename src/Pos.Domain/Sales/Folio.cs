using System.Globalization;

namespace Pos.Domain.Sales;

/// <summary>Formato y lectura del folio de venta: <c>V-000123</c>.</summary>
public static class Folio
{
    public static string Format(long number) =>
        string.Create(CultureInfo.InvariantCulture, $"V-{number:000000}");

    /// <summary>Acepta <c>V-000123</c>, <c>v123</c> o <c>123</c>; el número debe ser mayor que 0.</summary>
    public static bool TryParse(string? text, out long number)
    {
        number = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var value = text.Trim();
        if (value[0] is 'V' or 'v')
        {
            value = value[1..].TrimStart('-');
        }

        if (value.Length == 0 || value.Length > 15 || !value.All(char.IsAsciiDigit))
        {
            return false;
        }

        return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number) && number > 0;
    }
}
