using Pos.Domain.Common;

namespace Pos.Domain.Inventory;

/// <summary>
/// Registro inmutable de un cambio de existencia (FR-010). Solo lo crea
/// <see cref="ProductStock.Record"/>; no tiene métodos que lo modifiquen. <c>CreatedAt</c> y
/// <c>CreatedBy</c> los asigna la persistencia.
/// </summary>
public sealed class InventoryMovement
{
    public const int ReasonMaxLength = 250;
    public const int ReferenceMaxLength = 50;

    private InventoryMovement()
    {
    }

    internal InventoryMovement(
        Guid productId,
        int sequence,
        MovementType type,
        Quantity quantity,
        StockLevel resultingStock,
        string? reason,
        string? reference)
    {
        Id = Guid.CreateVersion7();
        ProductId = productId;
        Sequence = sequence;
        Type = type;
        QuantityThousandths = quantity.Thousandths;
        ResultingStockThousandths = resultingStock.Thousandths;
        Reason = NormalizeText(reason);
        Reference = NormalizeText(reference);
    }

    public Guid Id { get; private set; }

    public Guid ProductId { get; private set; }

    public int Sequence { get; private set; }

    public MovementType Type { get; private set; }

    /// <summary>Cantidad en milésimas, siempre positiva; el signo lo da el tipo.</summary>
    public long QuantityThousandths { get; private set; }

    /// <summary>Existencia después del movimiento, en milésimas (puede ser negativa por ventas).</summary>
    public long ResultingStockThousandths { get; private set; }

    public Quantity Quantity => Quantity.FromThousandths(QuantityThousandths);

    public StockLevel ResultingStock => StockLevel.FromThousandths(ResultingStockThousandths);

    public string? Reason { get; private set; }

    public string? Reference { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    /// <summary>Recorta; vacío o solo espacios se convierte en nulo.</summary>
    public static string? NormalizeText(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
