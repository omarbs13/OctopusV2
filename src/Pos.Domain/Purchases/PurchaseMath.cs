using Pos.Domain.Common;
using Pos.Domain.Sales;

namespace Pos.Domain.Purchases;

/// <summary>Importe que excede <see cref="Money.MaxCents"/> en una compra (research §4).</summary>
public enum PurchaseAmountLimit
{
    None,
    Line,
    Subtotal,
    Tax,
    Total,
}

/// <summary>Cálculos exactos de una compra (research §4). Todo en centavos y milésimas enteros.</summary>
public static class PurchaseMath
{
    /// <summary>
    /// Importe de una línea: <c>cantidad × costo</c> redondeado "mitad hacia arriba" al centavo, con la
    /// misma regla que la venta (<see cref="SaleMath.LineAmount"/>).
    /// </summary>
    /// <exception cref="DomainException">El importe excede el máximo permitido.</exception>
    public static long LineAmount(long quantityThousandths, long unitCostCents) =>
        SaleMath.LineAmount(Quantity.FromThousandths(quantityThousandths), Money.FromCents(unitCostCents)).Cents;

    /// <summary>Indica si el importe de la línea excedería <see cref="Money.MaxCents"/>, sin lanzar.</summary>
    public static bool LineExceedsMaximum(long quantityThousandths, long unitCostCents)
    {
        try
        {
            return checked(((quantityThousandths * unitCostCents) + 500) / 1000) > Money.MaxCents;
        }
        catch (OverflowException)
        {
            return true;
        }
    }

    /// <summary>Suma de los importes de línea ya redondeados.</summary>
    public static long Subtotal(IEnumerable<long> lineAmounts)
    {
        ArgumentNullException.ThrowIfNull(lineAmounts);
        long sum = 0;
        foreach (var amount in lineAmounts)
        {
            sum = checked(sum + amount);
        }

        return sum;
    }

    public static long Total(long subtotalCents, long taxCents) => checked(subtotalCents + taxCents);

    /// <summary>Primer importe que excede <see cref="Money.MaxCents"/>, o <see cref="PurchaseAmountLimit.None"/>.</summary>
    public static PurchaseAmountLimit ExceededLimit(IEnumerable<long> lineAmounts, long taxCents)
    {
        ArgumentNullException.ThrowIfNull(lineAmounts);
        var amounts = lineAmounts.ToList();
        if (amounts.Any(a => a > Money.MaxCents))
        {
            return PurchaseAmountLimit.Line;
        }

        var subtotal = Subtotal(amounts);
        if (subtotal > Money.MaxCents)
        {
            return PurchaseAmountLimit.Subtotal;
        }

        if (taxCents > Money.MaxCents)
        {
            return PurchaseAmountLimit.Tax;
        }

        return Total(subtotal, taxCents) > Money.MaxCents ? PurchaseAmountLimit.Total : PurchaseAmountLimit.None;
    }
}
