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
    /// Interpreta un importe capturado, después de recortar espacios de los extremos. Acepta dígitos
    /// con un punto decimal opcional, con o sin separador de miles con coma en grupos de 3
    /// ("1234.50" o "1,234.50"). Nunca redondea: más de 2 decimales es un error.
    /// </summary>
    public static MoneyParseResult Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return MoneyParseResult.Failure(MoneyParseError.Empty);
        }

        var match = PricePattern().Match(text.Trim());
        if (!match.Success)
        {
            return MoneyParseResult.Failure(MoneyParseError.Format);
        }

        var decimals = match.Groups["decimales"].Value;
        if (decimals.Length > 2)
        {
            return MoneyParseResult.Failure(MoneyParseError.TooManyDecimals);
        }

        // Sin ceros a la izquierda, más de 7 dígitos de pesos ya excede el máximo (y evita desbordes).
        var digits = match.Groups["pesos"].Value.Replace(",", string.Empty, StringComparison.Ordinal).TrimStart('0');
        if (digits.Length == 0)
        {
            digits = "0";
        }

        if (digits.Length > 7)
        {
            return MoneyParseResult.Failure(MoneyParseError.TooLarge);
        }

        var cents = (long.Parse(digits, CultureInfo.InvariantCulture) * 100)
            + long.Parse(decimals.PadRight(2, '0'), CultureInfo.InvariantCulture);
        return cents > MaxCents
            ? MoneyParseResult.Failure(MoneyParseError.TooLarge)
            : MoneyParseResult.Success(new Money(cents));
    }

    /// <summary>Atajo de <see cref="Parse"/> para quien solo necesita saber si el texto es válido.</summary>
    public static bool TryParse(string? text, out Money money)
    {
        var result = Parse(text);
        money = result.Value ?? Zero;
        return result.Value is not null;
    }

    /// <summary>Texto para edición: punto decimal, dos decimales y sin separador de miles.</summary>
    public string ToEditableString() =>
        (Cents / 100m).ToString("0.00", CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^(?<pesos>[0-9]+|[0-9]{1,3}(,[0-9]{3})+)(\.(?<decimales>[0-9]+))?$", RegexOptions.CultureInvariant)]
    private static partial Regex PricePattern();
}

/// <summary>Motivo por el que un importe capturado no es válido.</summary>
public enum MoneyParseError
{
    Empty,
    Format,
    TooManyDecimals,
    TooLarge,
}

/// <summary>Resultado de <see cref="Money.Parse"/>: un importe o un error.</summary>
public readonly record struct MoneyParseResult(Money? Value, MoneyParseError? Error)
{
    public static MoneyParseResult Success(Money value) => new(value, null);

    public static MoneyParseResult Failure(MoneyParseError error) => new(null, error);
}
