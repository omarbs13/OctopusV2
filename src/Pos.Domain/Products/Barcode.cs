using System.Text.RegularExpressions;

namespace Pos.Domain.Products;

/// <summary>
/// Reglas del código de barras (021): normalización, regla del catálogo, dígito verificador EAN y
/// clasificación de una lectura del escáner.
/// </summary>
public static partial class Barcode
{
    /// <summary>Largo máximo después de normalizar.</summary>
    public const int MaxLength = 48;

    /// <summary>
    /// Recorta los extremos; si empieza y termina con <c>*</c> (CODE39) y quedan caracteres, quita
    /// ambos asteriscos y vuelve a recortar; pasa a mayúsculas invariantes. Vacío se convierte en nulo.
    /// </summary>
    public static string? Normalize(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length > 2 && text[0] == '*' && text[^1] == '*')
        {
            text = text[1..^1].Trim();
        }

        return text.Length == 0 ? null : text.ToUpperInvariant();
    }

    /// <summary>
    /// Nulo es válido (el campo es opcional); si no, de 1 a 48 letras mayúsculas, dígitos, espacios
    /// interiores y los símbolos <c>- . $ / + %</c>.
    /// </summary>
    public static bool IsValidForCatalog(string? normalized) =>
        normalized is null || CatalogPattern().IsMatch(normalized);

    /// <summary>
    /// Dígito verificador EAN-13 o EAN-8: módulo 10 con pesos alternos 3, 1, 3, … de derecha a
    /// izquierda sin contar el dígito verificador. Falso para otro largo o caracteres que no son dígitos.
    /// </summary>
    public static bool HasValidEanCheckDigit(string digits)
    {
        ArgumentNullException.ThrowIfNull(digits);
        if (digits.Length is not (8 or 13) || !digits.All(char.IsAsciiDigit))
        {
            return false;
        }

        var sum = 0;
        for (var i = digits.Length - 2; i >= 0; i--)
        {
            var weight = (digits.Length - 2 - i) % 2 == 0 ? 3 : 1;
            sum += (digits[i] - '0') * weight;
        }

        return (10 - (sum % 10)) % 10 == digits[^1] - '0';
    }

    /// <summary>Normaliza y clasifica una lectura (research §4).</summary>
    public static BarcodeFormat Classify(string? raw)
    {
        var normalized = Normalize(raw);
        if (normalized is null)
        {
            return BarcodeFormat.Empty;
        }

        if (!IsValidForCatalog(normalized))
        {
            return BarcodeFormat.Unrecognized;
        }

        var allDigits = normalized.All(char.IsAsciiDigit);
        return (allDigits, normalized.Length) switch
        {
            (true, 13) => HasValidEanCheckDigit(normalized) ? BarcodeFormat.Ean13 : BarcodeFormat.Unrecognized,
            (true, 8) => HasValidEanCheckDigit(normalized) ? BarcodeFormat.Ean8 : BarcodeFormat.Unrecognized,
            _ => BarcodeFormat.Code128OrCode39,
        };
    }

    [GeneratedRegex(@"^[A-Z0-9 .$/+%-]{1,48}$", RegexOptions.CultureInvariant)]
    private static partial Regex CatalogPattern();
}
