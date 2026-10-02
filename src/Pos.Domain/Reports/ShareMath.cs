namespace Pos.Domain.Reports;

/// <summary>Participación de una parte en un total, sin punto flotante (016, research §10).</summary>
public static class ShareMath
{
    /// <summary>
    /// <paramref name="part"/> / <paramref name="total"/> en puntos base (10,000 = 100 %), mitad hacia
    /// arriba; 0 si el total es 0. No se ajusta para que varias partes sumen exactamente 100 %.
    /// </summary>
    public static long BasisPoints(long part, long total) =>
        total == 0 ? 0 : VariationMath.RoundedDivision(part * 10_000, total);
}
