using System.Globalization;

namespace Pos.Domain.CreditNotes;

/// <summary>Formato y lectura del folio de nota de crédito: <c>NC-000001</c>.</summary>
public static class CreditNoteFolio
{
    public static string Format(long number) =>
        string.Create(CultureInfo.InvariantCulture, $"NC-{number:000000}");

    /// <summary>Acepta <c>NC-000123</c>, <c>nc123</c> o <c>123</c>; el número debe ser mayor que 0.</summary>
    public static bool TryParse(string? text, out long number)
    {
        number = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var value = text.Trim();
        if (value.Length >= 2 && value[..2].Equals("NC", StringComparison.OrdinalIgnoreCase))
        {
            value = value[2..].TrimStart('-');
        }

        if (value.Length == 0 || value.Length > 15 || !value.All(char.IsAsciiDigit))
        {
            return false;
        }

        return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number) && number > 0;
    }
}
