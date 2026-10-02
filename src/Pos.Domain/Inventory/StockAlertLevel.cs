using Pos.Domain.Common;

namespace Pos.Domain.Inventory;

/// <summary>
/// Nivel de alerta de existencia de un producto según sus umbrales (022). Valores persistidos como
/// entero en <c>StockAlertAcknowledgements.Level</c>; no se renumeran.
/// </summary>
public enum StockAlertLevel
{
    None = 0,
    Alert = 1,
    Urgent = 2,
}

public static class StockAlertRule
{
    /// <summary>
    /// Urgente si hay punto de reorden y la existencia no lo supera; en alerta si hay mínimo y la
    /// existencia no lo supera; sin alerta en otro caso. Es independiente de
    /// <see cref="StockStatusRule"/>: un producto sin existencia también puede ser urgente o estar en
    /// alerta. Infrastructure replica este predicado en SQL (research §3).
    /// </summary>
    public static StockAlertLevel Evaluate(StockLevel onHand, Quantity? minimum, Quantity? reorderPoint)
    {
        if (reorderPoint is { } reorder && onHand <= reorder)
        {
            return StockAlertLevel.Urgent;
        }

        return minimum is { } min && onHand <= min ? StockAlertLevel.Alert : StockAlertLevel.None;
    }
}
