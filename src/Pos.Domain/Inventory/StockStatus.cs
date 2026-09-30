using Pos.Domain.Common;

namespace Pos.Domain.Inventory;

/// <summary>Estado de la existencia de un producto, deducido de la existencia y el mínimo.</summary>
public enum StockStatus
{
    Normal,
    Low,
    Out,
}

public static class StockStatusRule
{
    /// <summary>
    /// Sin existencia si es 0 o menor (las ventas pueden dejarla negativa); baja si es mayor que 0 y no supera el mínimo definido; normal en otro
    /// caso. Infrastructure replica este predicado en SQL (research §8).
    /// </summary>
    public static StockStatus Evaluate(StockLevel onHand, Quantity? minimum)
    {
        if (onHand <= StockLevel.Zero)
        {
            return StockStatus.Out;
        }

        return minimum is { } min && onHand <= min ? StockStatus.Low : StockStatus.Normal;
    }
}
