using Pos.Domain.Common;

namespace Pos.Domain.Inventory;

/// <summary>Tipo de movimiento de inventario. La cantidad siempre es positiva y el signo lo da el tipo.</summary>
public enum MovementType
{
    Initial,
    Receipt,
    AdjustIn,
    AdjustOut,
    Sale,
    SaleCancellation,
    SaleReturn,

    /// <summary>Entrada por una compra registrada (020); solo la genera <see cref="ProductStock.RecordPurchase"/>.</summary>
    Purchase,

    /// <summary>Salida por la anulación de una compra (020); solo la genera <see cref="ProductStock.RecordPurchaseVoid"/>.</summary>
    PurchaseVoid,
}

public static class MovementTypeExtensions
{
    public static bool IsIncrease(this MovementType type) =>
        type is not (MovementType.AdjustOut or MovementType.Sale or MovementType.PurchaseVoid);

    public static bool RequiresReason(this MovementType type) =>
        type is MovementType.AdjustIn or MovementType.AdjustOut;

    /// <summary>Código de texto estable que se guarda en la base (research §7).</summary>
    public static string ToCode(this MovementType type) => type switch
    {
        MovementType.Initial => "INITIAL",
        MovementType.Receipt => "RECEIPT",
        MovementType.AdjustIn => "ADJUST_IN",
        MovementType.AdjustOut => "ADJUST_OUT",
        MovementType.Sale => "SALE",
        MovementType.SaleCancellation => "SALE_CANCEL",
        MovementType.SaleReturn => "SALE_RETURN",
        MovementType.Purchase => "PURCHASE",
        MovementType.PurchaseVoid => "PURCH_VOID",
        _ => throw new DomainException("El tipo de movimiento no es válido."),
    };

    public static MovementType FromCode(string code) => code switch
    {
        "INITIAL" => MovementType.Initial,
        "RECEIPT" => MovementType.Receipt,
        "ADJUST_IN" => MovementType.AdjustIn,
        "ADJUST_OUT" => MovementType.AdjustOut,
        "SALE" => MovementType.Sale,
        "SALE_CANCEL" => MovementType.SaleCancellation,
        "SALE_RETURN" => MovementType.SaleReturn,
        "PURCHASE" => MovementType.Purchase,
        "PURCH_VOID" => MovementType.PurchaseVoid,
        _ => throw new DomainException("El tipo de movimiento no es válido."),
    };
}
