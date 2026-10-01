using System.Globalization;
using System.Text.RegularExpressions;
using Pos.Domain.Common;

namespace Pos.Domain.Discounts;

/// <summary>
/// Valor capturado de un descuento (015, research §1): porcentaje en puntos base (1–10 000 = 0.01 %–100 %)
/// o monto fijo en centavos (1–<see cref="Money.MaxCents"/>). Todo entero.
/// </summary>
public readonly partial record struct DiscountValue
{
    public const long MaxBasisPoints = 10_000;

    private DiscountValue(DiscountMode mode, long raw)
    {
        Mode = mode;
        Raw = raw;
    }

    public DiscountMode Mode { get; }

    /// <summary>Puntos base si es porcentaje; centavos si es monto.</summary>
    public long Raw { get; }

    public bool IsPercent => Mode == DiscountMode.Percent;

    public static DiscountValue Percent(long basisPoints) => Create(DiscountMode.Percent, basisPoints);

    public static DiscountValue Amount(long cents) => Create(DiscountMode.Amount, cents);

    /// <summary>Valida el rango según la modalidad.</summary>
    public static DiscountValue Create(DiscountMode mode, long raw)
    {
        switch (mode)
        {
            case DiscountMode.Percent when raw is < 1 or > MaxBasisPoints:
                throw new DomainException("El porcentaje debe ser mayor que 0 y no mayor que 100.");
            case DiscountMode.Amount when raw is < 1 or > Money.MaxCents:
                throw new DomainException("El monto del descuento debe ser mayor que 0.");
            case DiscountMode.Percent or DiscountMode.Amount:
                return new DiscountValue(mode, raw);
            default:
                throw new DomainException("La modalidad del descuento no es válida.");
        }
    }

    /// <summary>
    /// Interpreta lo capturado: "12.5" → 1 250 pb, "15" → $15.00. Admite como máximo 2 decimales y nunca
    /// redondea (como <see cref="Money.Parse"/>). Lanza <see cref="DomainException"/> con un mensaje claro.
    /// </summary>
    public static DiscountValue Parse(DiscountMode mode, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new DomainException("Capture el valor del descuento.");
        }

        if (mode == DiscountMode.Amount)
        {
            var parsed = Money.Parse(text);
            return parsed.Error switch
            {
                MoneyParseError.TooManyDecimals => throw new DomainException("El monto admite hasta 2 decimales."),
                not null => throw new DomainException("El monto del descuento no es válido."),
                _ => Create(DiscountMode.Amount, parsed.Value!.Value.Cents),
            };
        }

        var match = PercentPattern().Match(text.Trim());
        if (!match.Success)
        {
            throw new DomainException("El porcentaje no es válido.");
        }

        var decimals = match.Groups["decimales"].Value;
        if (decimals.Length > 2)
        {
            throw new DomainException("El porcentaje admite hasta 2 decimales.");
        }

        var whole = long.Parse(match.Groups["entero"].Value, CultureInfo.InvariantCulture);
        if (whole > 100)
        {
            throw new DomainException("El porcentaje debe ser mayor que 0 y no mayor que 100.");
        }

        var basisPoints = (whole * 100) + long.Parse(decimals.PadRight(2, '0'), CultureInfo.InvariantCulture);
        return Create(DiscountMode.Percent, basisPoints);
    }

    /// <summary>"10%", "12.5%" o "$15.00".</summary>
    public override string ToString() => Mode == DiscountMode.Percent
        ? FormatPercent(Raw)
        : "$" + (Raw / 100m).ToString("N2", CultureInfo.InvariantCulture);

    /// <summary>Puntos base como porcentaje sin ceros de sobra: 1000 → "10%", 1250 → "12.5%".</summary>
    public static string FormatPercent(long basisPoints) =>
        (basisPoints / 100m).ToString("0.##", CultureInfo.InvariantCulture) + "%";

    /// <summary>Texto para edición: "12.5" o "15.00".</summary>
    public string ToEditableString() => Mode == DiscountMode.Percent
        ? (Raw / 100m).ToString("0.##", CultureInfo.InvariantCulture)
        : (Raw / 100m).ToString("0.00", CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^(?<entero>[0-9]{1,3})(\.(?<decimales>[0-9]+))?$", RegexOptions.CultureInvariant)]
    private static partial Regex PercentPattern();
}
