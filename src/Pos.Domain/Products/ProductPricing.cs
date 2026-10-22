using Pos.Domain.Common;
using Pos.Domain.Sales;

namespace Pos.Domain.Products;

/// <summary>
/// Precios de loseta derivados entre sí con el embalaje (m² por caja). Todo es entero, con redondeo
/// "mitad hacia arriba" al centavo (026, research §5).
/// </summary>
public static class ProductPricing
{
    /// <summary>Diferencia, como porcentaje, a partir de la cual se advierte entre X CAJA y X M2 × Embalaje.</summary>
    public const int TolerancePercent = 2;

    /// <summary>X CAJA sugerido: X M2 × Embalaje. Nulo si falta algún dato.</summary>
    public static long? SuggestBoxPrice(long? sqmCents, long? packagingThousandths) =>
        sqmCents is > 0 && packagingThousandths is > 0
            ? RoundedDiv(checked(sqmCents.Value * packagingThousandths.Value), 1000)
            : null;

    /// <summary>X M2 sugerido: X CAJA ÷ Embalaje. Nulo si falta algún dato.</summary>
    public static long? SuggestSquareMeterPrice(long? boxCents, long? packagingThousandths) =>
        boxCents is > 0 && packagingThousandths is > 0
            ? RoundedDiv(checked(boxCents.Value * 1000), packagingThousandths.Value)
            : null;

    /// <summary>Indica si X CAJA difiere de X M2 × Embalaje en más del 2 % de este último.</summary>
    public static bool ExceedsTolerance(long boxCents, long sqmCents, long packagingThousandths)
    {
        var expected = checked(sqmCents * packagingThousandths);
        var difference = Math.Abs(checked(boxCents * 1000) - expected);
        return checked(difference * 100) > checked(expected * TolerancePercent);
    }

    /// <summary>
    /// Precio unitario para una modalidad: caja = X CAJA o X M2 × Embalaje; metro cuadrado = X M2 o
    /// X CAJA ÷ Embalaje; pieza = el precio por pieza (sin derivar). Con <paramref name="isTile"/> falso solo
    /// existe el precio por pieza. Nulo si no hay precio o excede el máximo.
    /// </summary>
    public static long? PriceFor(
        SaleMode mode,
        bool isTile,
        long? sqmCents,
        long? boxCents,
        long? pieceCents,
        long? packagingThousandths)
    {
        if (!isTile)
        {
            return mode == SaleMode.Piece ? pieceCents : null;
        }

        var cents = mode switch
        {
            SaleMode.Box => boxCents ?? SuggestBoxPrice(sqmCents, packagingThousandths),
            SaleMode.SquareMeter => sqmCents ?? SuggestSquareMeterPrice(boxCents, packagingThousandths),
            _ => pieceCents,
        };
        return cents is > 0 and <= Money.MaxCents ? cents : null;
    }

    /// <summary>Modalidad con la que inicia una partida: la de la unidad del producto.</summary>
    public static SaleMode InitialMode(string unitCode) => unitCode switch
    {
        "XBX" => SaleMode.Box,
        "MTK" => SaleMode.SquareMeter,
        _ => SaleMode.Piece,
    };

    /// <summary>Precio con el que se muestra un producto: el de la modalidad de su unidad; nulo si no se puede determinar.</summary>
    public static long? DisplayPrice(
        string unitCode,
        long? sqmCents,
        long? boxCents,
        long? pieceCents,
        long? packagingThousandths) =>
        PriceFor(InitialMode(unitCode), UnitOfMeasure.IsTile(unitCode), sqmCents, boxCents, pieceCents, packagingThousandths);

    private static long RoundedDiv(long numerator, long denominator) =>
        (numerator + (denominator / 2)) / denominator;
}
