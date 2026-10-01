using Pos.Domain.Common;

namespace Pos.Domain.Discounts;

/// <summary>Cálculos exactos de descuentos, todo en enteros (015, research §2–§3).</summary>
public static class DiscountMath
{
    /// <summary>
    /// Monto del descuento sobre <paramref name="baseCents"/>. Porcentaje: <c>(base × pb + 5 000) / 10 000</c>
    /// (mitad hacia arriba, como <c>SaleMath.LineAmount</c>; con importes positivos equivale a "mitad
    /// alejándose de cero"). Monto: el valor. Rechaza un resultado mayor que la base o de 0 centavos.
    /// </summary>
    public static long Amount(long baseCents, DiscountValue value)
    {
        if (baseCents is < 0 or > Money.MaxCents)
        {
            throw new DomainException("El importe no es válido.");
        }

        var amount = value.Mode == DiscountMode.Percent
            ? ((baseCents * value.Raw) + 5_000) / DiscountValue.MaxBasisPoints
            : value.Raw;

        if (amount > baseCents)
        {
            throw new DomainException("El descuento no puede ser mayor que el importe.");
        }

        if (amount == 0)
        {
            throw new DomainException("El descuento resulta en $0.00.");
        }

        return amount;
    }

    /// <summary>
    /// Indica si el descuento supera el límite: <c>descuento × 10 000 &gt; límite × base</c>. Multiplicación
    /// cruzada entera: un descuento exactamente igual al límite no lo supera (spec, casos límite).
    /// </summary>
    public static bool ExceedsLimit(long discountCents, long baseCents, long limitBasisPoints)
    {
        if (baseCents <= 0 || discountCents <= 0)
        {
            return false;
        }

        return (Int128)discountCents * DiscountValue.MaxBasisPoints > (Int128)limitBasisPoints * baseCents;
    }

    /// <summary>
    /// Porcentaje equivalente en puntos base, redondeado hacia arriba: una aprobación por este valor
    /// siempre cubre el descuento. Se guarda en la aprobación (research §7).
    /// </summary>
    public static long EquivalentBasisPoints(long discountCents, long baseCents)
    {
        if (baseCents <= 0 || discountCents <= 0)
        {
            return 0;
        }

        var product = (Int128)discountCents * DiscountValue.MaxBasisPoints;
        var result = (long)((product + baseCents - 1) / baseCents);
        return Math.Min(result, DiscountValue.MaxBasisPoints);
    }
}
