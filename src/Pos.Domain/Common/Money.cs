using System.Globalization;
using System.Text.RegularExpressions;

namespace Pos.Domain.Common;

/// <summary>
/// Importe en pesos mexicanos, representado en centavos enteros (constitución, Principio IV).
/// </summary>
public readonly partial record struct Money
{
    /// <summary>999,999.99 pesos.</summary>
    public const long MaxCents = 99_999_999;

    private Money(long cents) => Cents = cents;

    public long Cents { get; }

    public static Money Zero => default;

    public static Money FromCents(long cents)
    {
        if (cents is < 0 or > MaxCents)
        {
            throw new DomainException($"El importe debe estar entre 0 y {MaxCents} centavos.");
        }

        return new Money(cents);
    }

    /// <summary>
    /// Acepta solo dígitos y un punto decimal opcional con 1 o 2 decimales (por ejemplo "1234.50"),
    /// después de recortar espacios de los extremos. Nunca redondea.
    /// </summary>
    public static bool TryParse(string? text, out Money money)
    {
        money = Zero;
        if (text is null)
        {
            return false;
        }

        var match = PricePattern().Match(text.Trim());
        if (!match.Success)
        {
            return false;
        }

        var pesos = long.Parse(match.Groups["pesos"].Value, CultureInfo.InvariantCulture);
        var decimals = match.Groups["centavos"].Value.PadRight(2, '0');
        var centavos = long.Parse(decimals, CultureInfo.InvariantCulture);

        money = new Money((pesos * 100) + centavos);
        return true;
    }

    /// <summary>Texto para edición: punto decimal, dos decimales y sin separador de miles.</summary>
    public string ToEditableString() =>
        (Cents / 100m).ToString("0.00", CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^(?<pesos>[0-9]{1,6})(\.(?<centavos>[0-9]{1,2}))?$", RegexOptions.CultureInvariant)]
    private static partial Regex PricePattern();
}
