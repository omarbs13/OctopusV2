namespace Pos.Domain.Inventory;

/// <summary>
/// Cantidad que regresa a la existencia al devolver o cancelar parte de una línea vendida en una unidad distinta
/// a la del producto (026). Se reparte el movimiento original en proporción a lo devuelto, de forma acumulada,
/// de modo que devolver toda la línea en partes regresa exactamente lo que salió, sin residuos de redondeo. Con
/// la misma unidad el movimiento y la línea valen igual y el resultado es la cantidad devuelta.
/// </summary>
public static class SaleStockRestore
{
    /// <param name="movementThousandths">Cantidad del movimiento <c>SALE</c>, en la unidad del producto.</param>
    /// <param name="lineThousandths">Cantidad vendida de la línea, en la unidad vendida.</param>
    /// <param name="alreadyReturnedThousandths">Cantidad ya devuelta de la línea, en la unidad vendida.</param>
    /// <param name="returningThousandths">Cantidad que se devuelve ahora, en la unidad vendida.</param>
    public static long Quantity(long movementThousandths, long lineThousandths, long alreadyReturnedThousandths, long returningThousandths)
    {
        if (lineThousandths <= 0)
        {
            return 0;
        }

        return Share(movementThousandths, lineThousandths, alreadyReturnedThousandths + returningThousandths)
            - Share(movementThousandths, lineThousandths, alreadyReturnedThousandths);
    }

    private static long Share(long movement, long line, long returned) => ((movement * returned * 2) + line) / (2 * line);
}
