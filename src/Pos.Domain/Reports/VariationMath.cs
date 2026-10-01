namespace Pos.Domain.Reports;

/// <summary>Variación porcentual entre dos totales, sin punto flotante (research §3).</summary>
public static class VariationMath
{
    /// <summary>
    /// Variación (actual − anterior) / anterior en centésimas de por ciento (1,250 = 12.50 %), redondeada
    /// a media hacia afuera de cero; nulo si el anterior es 0 (no calculable).
    /// </summary>
    public static long? PercentBasisPoints(long previous, long current)
    {
        if (previous == 0)
        {
            return null;
        }

        return RoundedDivision((current - previous) * 10_000, previous);
    }

    internal static long RoundedDivision(long numerator, long denominator)
    {
        var quotient = Math.DivRem(numerator, denominator, out var remainder);
        if (Math.Abs(remainder) * 2 >= Math.Abs(denominator))
        {
            quotient += (numerator < 0) == (denominator < 0) ? 1 : -1;
        }

        return quotient;
    }
}
