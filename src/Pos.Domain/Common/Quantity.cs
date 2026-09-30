using System.Globalization;
using System.Text.RegularExpressions;

namespace Pos.Domain.Common;

/// <summary>
/// Cantidad de inventario, representada en milésimas enteras para que la suma y la comparación
/// sean exactas (constitución, Principio IV). La unidad de medida decide cuántos decimales se
/// aceptan al capturar (0 o 3).
/// </summary>
public readonly partial record struct Quantity : IComparable<Quantity>
{
    /// <summary>9,999,999.999: máximo de una cantidad capturada.</summary>
    public const long MaxCaptureThousandths = 9_999_999_999;

    /// <summary>999,999,999.999: máximo de una existencia.</summary>
    public const long MaxStockThousandths = 999_999_999_999;

    private const int MaxIntegerDigits = 7;

    private Quantity(long thousandths) => Thousandths = thousandths;

    public long Thousandths { get; }

    public static Quantity Zero => default;

    /// <summary>Crea una cantidad sin negativos ni por encima de <see cref="MaxStockThousandths"/>.</summary>
    public static Quantity FromThousandths(long thousandths)
    {
        if (thousandths is < 0 or > MaxStockThousandths)
        {
            throw new DomainException($"La cantidad debe estar entre 0 y {MaxStockThousandths} milésimas.");
        }

        return new Quantity(thousandths);
    }

    /// <summary>
    /// Interpreta una cantidad capturada, después de recortar espacios. Acepta dígitos con punto
    /// decimal opcional y separador de miles con coma en grupos de 3. Nunca redondea: más decimales
    /// de los permitidos es un error. Con <paramref name="allowZero"/> falso, 0 es <see cref="QuantityParseError.NotPositive"/>.
    /// </summary>
    public static QuantityParseResult Parse(string? text, int decimalPlaces, bool allowZero = false)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return QuantityParseResult.Failure(QuantityParseError.Empty);
        }

        var match = QuantityPattern().Match(text.Trim());
        if (!match.Success)
        {
            return QuantityParseResult.Failure(QuantityParseError.Format);
        }

        var decimals = match.Groups["decimales"].Value;
        if (decimals.Length > Math.Clamp(decimalPlaces, 0, 3))
        {
            return QuantityParseResult.Failure(QuantityParseError.TooManyDecimals);
        }

        var digits = match.Groups["enteros"].Value.Replace(",", string.Empty, StringComparison.Ordinal).TrimStart('0');
        if (digits.Length == 0)
        {
            digits = "0";
        }

        if (digits.Length > MaxIntegerDigits)
        {
            return QuantityParseResult.Failure(QuantityParseError.TooLarge);
        }

        var thousandths = (long.Parse(digits, CultureInfo.InvariantCulture) * 1000)
            + long.Parse(decimals.PadRight(3, '0'), CultureInfo.InvariantCulture);
        if (thousandths > MaxCaptureThousandths)
        {
            return QuantityParseResult.Failure(QuantityParseError.TooLarge);
        }

        return thousandths == 0 && !allowZero
            ? QuantityParseResult.Failure(QuantityParseError.NotPositive)
            : QuantityParseResult.Success(new Quantity(thousandths));
    }

    /// <summary>Indica si la cantidad no tiene más decimales que los permitidos.</summary>
    public bool FitsDecimals(int decimalPlaces)
    {
        var places = Math.Clamp(decimalPlaces, 0, 3);
        var step = (long)Math.Pow(10, 3 - places);
        return Thousandths % step == 0;
    }

    /// <summary>Texto para edición: "12" con 0 decimales, "1.250" con 3; sin separador de miles.</summary>
    public string ToEditableString(int decimalPlaces)
    {
        var places = Math.Clamp(decimalPlaces, 0, 3);
        return (Thousandths / 1000m).ToString(places == 0 ? "0" : "0." + new string('0', places), CultureInfo.InvariantCulture);
    }

    public int CompareTo(Quantity other) => Thousandths.CompareTo(other.Thousandths);

    public static Quantity operator +(Quantity left, Quantity right) =>
        new(checked(left.Thousandths + right.Thousandths));

    public static Quantity operator -(Quantity left, Quantity right) =>
        new(checked(left.Thousandths - right.Thousandths));

    public static bool operator <(Quantity left, Quantity right) => left.Thousandths < right.Thousandths;

    public static bool operator >(Quantity left, Quantity right) => left.Thousandths > right.Thousandths;

    public static bool operator <=(Quantity left, Quantity right) => left.Thousandths <= right.Thousandths;

    public static bool operator >=(Quantity left, Quantity right) => left.Thousandths >= right.Thousandths;

    [GeneratedRegex(@"^(?<enteros>[0-9]+|[0-9]{1,3}(,[0-9]{3})+)(\.(?<decimales>[0-9]+))?$", RegexOptions.CultureInvariant)]
    private static partial Regex QuantityPattern();
}

/// <summary>Motivo por el que una cantidad capturada no es válida.</summary>
public enum QuantityParseError
{
    Empty,
    Format,
    TooManyDecimals,
    TooLarge,
    NotPositive,
}

/// <summary>Resultado de <see cref="Quantity.Parse"/>: una cantidad o un error.</summary>
public readonly record struct QuantityParseResult(Quantity? Value, QuantityParseError? Error)
{
    public static QuantityParseResult Success(Quantity value) => new(value, null);

    public static QuantityParseResult Failure(QuantityParseError error) => new(null, error);
}
