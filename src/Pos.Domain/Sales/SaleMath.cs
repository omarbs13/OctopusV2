using Pos.Domain.Common;

namespace Pos.Domain.Sales;

/// <summary>Cálculos exactos de una venta (research §2).</summary>
public static class SaleMath
{
    /// <summary>
    /// Importe de una línea: <c>cantidad × precio</c> redondeado "mitad hacia arriba" una sola vez.
    /// Todo es entero: milésimas por centavos, entre 1,000.
    /// </summary>
    public static Money LineAmount(Quantity quantity, Money unitPrice)
    {
        long cents;
        try
        {
            cents = checked(((quantity.Thousandths * unitPrice.Cents) + 500) / 1000);
        }
        catch (OverflowException)
        {
            throw new DomainException("El importe de la línea excede el máximo permitido.");
        }

        if (cents > Money.MaxCents)
        {
            throw new DomainException("El importe de la línea excede el máximo permitido.");
        }

        return Money.FromCents(cents);
    }
}
