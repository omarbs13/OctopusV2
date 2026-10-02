using Pos.Desktop.Resources;
using Pos.Domain.Inventory;

namespace Pos.Desktop.Inventory;

/// <summary>Etiqueta de un tipo de movimiento en la interfaz.</summary>
public sealed record MovementTypeOption(MovementType? Type, string Label);

internal static class MovementTypeLabels
{
    public static string Of(MovementType type) => type switch
    {
        MovementType.Initial => Strings.MovementType_Initial,
        MovementType.Receipt => Strings.MovementType_Receipt,
        MovementType.AdjustIn => Strings.MovementType_AdjustIn,
        MovementType.Sale => Strings.MovementType_Sale,
        MovementType.SaleCancellation => Strings.MovementType_SaleCancellation,
        MovementType.SaleReturn => Strings.MovementType_SaleReturn,
        MovementType.Purchase => Strings.MovementType_Purchase,
        MovementType.PurchaseVoid => Strings.MovementType_PurchaseVoid,
        _ => Strings.MovementType_AdjustOut,
    };
}
