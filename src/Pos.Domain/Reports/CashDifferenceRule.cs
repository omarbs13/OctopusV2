namespace Pos.Domain.Reports;

/// <summary>Porcentaje y alerta de la diferencia de arqueo de un turno cerrado (research §3).</summary>
public static class CashDifferenceRule
{
    /// <summary>Umbral por defecto de alerta: 5 % (500 centésimas de por ciento).</summary>
    public const long DefaultThresholdBasisPoints = 500;

    /// <summary>
    /// Diferencia entre el efectivo esperado, en centésimas de por ciento, redondeada a media hacia afuera
    /// de cero; nulo si el esperado es 0 o negativo (no calculable).
    /// </summary>
    public static long? PercentBasisPoints(long differenceCents, long expectedCents)
    {
        if (expectedCents <= 0)
        {
            return null;
        }

        return VariationMath.RoundedDivision(differenceCents * 10_000, expectedCents);
    }

    /// <summary>Alerta si el valor absoluto supera el umbral; exactamente el umbral no es alerta.</summary>
    public static bool IsAlert(long? basisPoints, long thresholdBasisPoints) =>
        basisPoints is { } value && Math.Abs(value) > thresholdBasisPoints;
}
