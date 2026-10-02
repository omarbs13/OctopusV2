using Pos.Domain.Common;
using Pos.Domain.Products;

namespace Pos.Domain.Purchases;

/// <summary>Datos de una línea al registrar la compra, con los datos del producto ya congelados.</summary>
public sealed record PurchaseLineDraft(
    Guid ProductId,
    string ProductName,
    string ProductSku,
    string UnitCode,
    long QuantityThousandths,
    long UnitCostCents);

/// <summary>
/// Línea de una compra, inmutable (research §10). Es parte de <see cref="Purchase"/>; solo
/// <see cref="VoidMovementId"/> pasa de nulo a un valor al anular. <see cref="IsBonus"/> no se guarda.
/// </summary>
public sealed class PurchaseLine
{
    private PurchaseLine()
    {
        ProductName = string.Empty;
        ProductSku = string.Empty;
        UnitCode = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid PurchaseId { get; private set; }

    /// <summary>1..N en el orden de captura.</summary>
    public int LineNumber { get; private set; }

    public Guid ProductId { get; private set; }

    public string ProductName { get; private set; }

    public string ProductSku { get; private set; }

    public string UnitCode { get; private set; }

    public long QuantityThousandths { get; private set; }

    /// <summary>Costo unitario antes de impuestos; 0 = bonificación (FR-008).</summary>
    public long UnitCostCents { get; private set; }

    public long AmountCents { get; private set; }

    /// <summary>Movimiento <c>PURCHASE</c> que generó la línea (FR-013).</summary>
    public Guid MovementId { get; private set; }

    /// <summary>Movimiento <c>PURCH_VOID</c> de la anulación; nulo mientras la compra está vigente.</summary>
    public Guid? VoidMovementId { get; private set; }

    public bool IsBonus => UnitCostCents == 0;

    internal static PurchaseLine Create(Guid purchaseId, int lineNumber, PurchaseLineDraft draft)
    {
        if (draft.ProductId == Guid.Empty)
        {
            throw new DomainException("El producto de la línea no es válido.");
        }

        var name = (draft.ProductName ?? string.Empty).Trim();
        var sku = (draft.ProductSku ?? string.Empty).Trim();
        var unit = (draft.UnitCode ?? string.Empty).Trim();
        if (name.Length is 0 or > Product.NameMaxLength
            || sku.Length is 0 or > Product.SkuMaxLength
            || unit.Length is 0 or > UnitOfMeasure.CodeMaxLength)
        {
            throw new DomainException("Los datos del producto de la línea no son válidos.");
        }

        if (draft.QuantityThousandths <= 0 || draft.QuantityThousandths > Quantity.MaxCaptureThousandths)
        {
            throw new DomainException("La cantidad debe ser mayor que 0 y no exceder el máximo permitido.");
        }

        if (draft.UnitCostCents is < 0 or > Money.MaxCents)
        {
            throw new DomainException("El costo unitario debe ser mayor o igual que 0 y no exceder el máximo permitido.");
        }

        return new PurchaseLine
        {
            Id = Guid.CreateVersion7(),
            PurchaseId = purchaseId,
            LineNumber = lineNumber,
            ProductId = draft.ProductId,
            ProductName = name,
            ProductSku = sku,
            UnitCode = unit,
            QuantityThousandths = draft.QuantityThousandths,
            UnitCostCents = draft.UnitCostCents,
            AmountCents = PurchaseMath.LineAmount(draft.QuantityThousandths, draft.UnitCostCents),
        };
    }

    internal void LinkMovement(Guid movementId)
    {
        if (movementId == Guid.Empty || MovementId != Guid.Empty)
        {
            throw new DomainException("El movimiento de la línea ya está asignado o no es válido.");
        }

        MovementId = movementId;
    }

    internal void LinkVoidMovement(Guid movementId)
    {
        if (movementId == Guid.Empty || VoidMovementId is not null)
        {
            throw new DomainException("El movimiento de anulación de la línea ya está asignado o no es válido.");
        }

        VoidMovementId = movementId;
    }
}
