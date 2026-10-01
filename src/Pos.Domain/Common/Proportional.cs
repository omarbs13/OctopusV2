namespace Pos.Domain.Common;

/// <summary>Reparto proporcional exacto en enteros (extraído de <c>ReturnMath.Allocate</c>, 015 research §4).</summary>
public static class Proportional
{
    /// <summary>
    /// Reparte <paramref name="totalCents"/> en proporción a <paramref name="weights"/> por resto mayor. Cada
    /// peso es también el tope de su elemento: ninguno recibe más que su peso. Empata por orden y la suma
    /// es exacta. Devuelve los montos en el orden de <paramref name="weights"/>.
    /// </summary>
    public static IReadOnlyList<long> Allocate(long totalCents, IReadOnlyList<long> weights)
    {
        ArgumentNullException.ThrowIfNull(weights);
        var sum = weights.Sum();
        if (totalCents < 0 || weights.Any(r => r < 0) || totalCents > sum)
        {
            throw new DomainException("El monto a repartir excede la base.");
        }

        var result = new long[weights.Count];
        if (totalCents == 0)
        {
            return result;
        }

        var fractions = new Int128[weights.Count];
        long assigned = 0;
        for (var i = 0; i < weights.Count; i++)
        {
            var product = (Int128)totalCents * weights[i];
            result[i] = (long)(product / sum);
            fractions[i] = product % sum;
            assigned += result[i];
        }

        var order = Enumerable.Range(0, weights.Count)
            .OrderByDescending(i => fractions[i])
            .ThenBy(i => i)
            .ToList();
        var leftover = totalCents - assigned;
        foreach (var index in order)
        {
            if (leftover == 0)
            {
                break;
            }

            if (result[index] < weights[index])
            {
                result[index]++;
                leftover--;
            }
        }

        return result;
    }
}
