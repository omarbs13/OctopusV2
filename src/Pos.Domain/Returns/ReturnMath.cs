using Pos.Domain.Common;

namespace Pos.Domain.Returns;

/// <summary>Cálculos exactos de devoluciones, todo en enteros (research §3).</summary>
public static class ReturnMath
{
    /// <summary>
    /// Monto a devolver de una línea. Lo devuelto acumulado tras <c>q</c> unidades es
    /// <c>round(importeLínea × q / cantidadVendida)</c> (mitad hacia arriba); el monto de esta
    /// devolución es el acumulado nuevo menos el previo, así que devolver todo suma exactamente el
    /// importe de la línea.
    /// </summary>
    public static long LineRefund(long lineAmountCents, long soldThousandths, long returnedBeforeThousandths, long returningThousandths)
    {
        if (lineAmountCents < 0 || soldThousandths <= 0 || returnedBeforeThousandths < 0 || returningThousandths <= 0
            || returnedBeforeThousandths + returningThousandths > soldThousandths)
        {
            throw new DomainException("La cantidad a devolver no es válida.");
        }

        return Cumulative(lineAmountCents, soldThousandths, returnedBeforeThousandths + returningThousandths)
            - Cumulative(lineAmountCents, soldThousandths, returnedBeforeThousandths);
    }

    /// <summary>
    /// Reparte <paramref name="totalCents"/> entre las formas de pago por resto mayor, ponderando por lo
    /// que aún puede devolverse de cada una. Ninguna recibe más de su tope, empata por orden de captura
    /// y la suma es exacta. Devuelve los montos en el orden de <paramref name="remainingCents"/>.
    /// </summary>
    public static IReadOnlyList<long> Allocate(long totalCents, IReadOnlyList<long> remainingCents)
    {
        ArgumentNullException.ThrowIfNull(remainingCents);
        if (totalCents < 0 || remainingCents.Any(r => r < 0) || totalCents > remainingCents.Sum())
        {
            throw new DomainException("El monto a devolver excede lo pagado.");
        }

        return Proportional.Allocate(totalCents, remainingCents);
    }

    private static long Cumulative(long amountCents, long soldThousandths, long thousandths) =>
        (long)((((Int128)amountCents * thousandths * 2) + soldThousandths) / (2 * (Int128)soldThousandths));
}
