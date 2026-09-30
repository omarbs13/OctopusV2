using Pos.Domain.Common;
using Pos.Domain.Products;

namespace Pos.Domain.Inventory;

/// <summary>
/// Existencia actual de un producto. La fila se crea con su primer movimiento; sin fila, la
/// existencia es 0 y el producto no tiene movimientos (research §4).
/// </summary>
public sealed class ProductStock
{
    private ProductStock()
    {
    }

    public Guid ProductId { get; private set; }

    /// <summary>Existencia en milésimas; es lo que se guarda, para sumar y comparar en SQL.</summary>
    public long OnHandThousandths { get; private set; }

    /// <summary>Existencia con signo: las ventas pueden dejarla negativa (research §3).</summary>
    public StockLevel OnHand => StockLevel.FromThousandths(OnHandThousandths);

    public int MovementCount { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public Guid UpdatedBy { get; private set; }

    public int Version { get; private set; }

    /// <summary>Existencia en 0 y sin movimientos, justo antes de registrar el primero.</summary>
    public static ProductStock Start(Guid productId) =>
        new() { ProductId = productId, Version = 1 };

    /// <summary>El inventario inicial solo se permite si el producto no tiene movimientos (FR-015).</summary>
    public static bool CanRecordInitial(int movementCount) => movementCount == 0;

    /// <summary>Indica si restar <paramref name="quantity"/> dejaría la existencia bajo cero (FR-011).</summary>
    public static bool WouldGoNegative(StockLevel onHand, Quantity quantity) => quantity > onHand;

    /// <summary>Indica si sumar <paramref name="quantity"/> superaría la existencia máxima.</summary>
    public static bool WouldExceedMaximum(StockLevel onHand, Quantity quantity) =>
        onHand.Thousandths + quantity.Thousandths > Quantity.MaxStockThousandths;

    /// <summary>Indica si la cantidad pedida supera la existencia actual (advertencia de venta, FR-025).</summary>
    public static bool IsShort(StockLevel onHand, Quantity requested) => requested > onHand;

    /// <summary>
    /// Registra un movimiento: valida las reglas, actualiza la existencia y devuelve el movimiento
    /// inmutable con la existencia resultante.
    /// </summary>
    public InventoryMovement Record(
        MovementType type,
        Quantity quantity,
        UnitOfMeasure unit,
        bool productActive,
        bool tracksInventory,
        string? reason,
        string? reference)
    {
        ArgumentNullException.ThrowIfNull(unit);

        if (type is MovementType.Sale or MovementType.SaleCancellation)
        {
            throw new DomainException("Los movimientos de venta solo se generan al vender o cancelar una venta.");
        }

        if (!tracksInventory)
        {
            throw new DomainException("Este producto no controla inventario.");
        }

        if (!productActive)
        {
            throw new DomainException("El producto está inactivo; actívelo para registrar movimientos.");
        }

        if (quantity <= Quantity.Zero || quantity.Thousandths > Quantity.MaxCaptureThousandths)
        {
            throw new DomainException("La cantidad debe ser mayor que 0 y no exceder el máximo permitido.");
        }

        if (!quantity.FitsDecimals(unit.DecimalPlaces))
        {
            throw new DomainException($"La cantidad excede los decimales que admite la unidad {unit.Name}.");
        }

        var normalizedReason = InventoryMovement.NormalizeText(reason);
        if (type.RequiresReason() && normalizedReason is null)
        {
            throw new DomainException("El motivo es obligatorio en los ajustes.");
        }

        if (normalizedReason is { Length: > InventoryMovement.ReasonMaxLength }
            || InventoryMovement.NormalizeText(reference) is { Length: > InventoryMovement.ReferenceMaxLength })
        {
            throw new DomainException("El motivo o la referencia exceden la longitud permitida.");
        }

        if (type == MovementType.Initial && !CanRecordInitial(MovementCount))
        {
            throw new DomainException("Este producto ya tiene movimientos; registre un ajuste en lugar de inventario inicial.");
        }

        StockLevel resulting;
        if (type.IsIncrease())
        {
            if (WouldExceedMaximum(OnHand, quantity))
            {
                throw new DomainException("La existencia excedería el máximo permitido.");
            }

            resulting = OnHand + quantity;
        }
        else
        {
            if (WouldGoNegative(OnHand, quantity))
            {
                throw new DomainException("El ajuste dejaría la existencia por debajo de cero.");
            }

            resulting = OnHand - quantity;
        }

        var movement = new InventoryMovement(ProductId, MovementCount + 1, type, quantity, resulting, reason, reference);
        OnHandThousandths = resulting.Thousandths;
        MovementCount++;
        return movement;
    }

    /// <summary>
    /// Registra la salida por una venta (<c>SALE</c>). Es el único movimiento que puede dejar la
    /// existencia negativa (FR-025). La referencia es el folio de la venta.
    /// </summary>
    public InventoryMovement RecordSale(Quantity quantity, UnitOfMeasure unit, string reference)
    {
        ArgumentNullException.ThrowIfNull(unit);
        ValidateSaleQuantity(quantity, unit, reference);

        var resulting = OnHand - quantity;
        var movement = new InventoryMovement(ProductId, MovementCount + 1, MovementType.Sale, quantity, resulting, null, reference);
        OnHandThousandths = resulting.Thousandths;
        MovementCount++;
        return movement;
    }

    /// <summary>
    /// Regresa la existencia que salió por una venta cancelada (<c>SALE_CANCEL</c>). No revisa el
    /// estado ni la configuración actual del producto (research §9).
    /// </summary>
    public InventoryMovement RecordSaleCancellation(Quantity quantity, string reference)
    {
        if (quantity <= Quantity.Zero)
        {
            throw new DomainException("La cantidad debe ser mayor que 0.");
        }

        if (InventoryMovement.NormalizeText(reference) is { Length: > InventoryMovement.ReferenceMaxLength })
        {
            throw new DomainException("La referencia excede la longitud permitida.");
        }

        if (WouldExceedMaximum(OnHand, quantity))
        {
            throw new DomainException("La existencia excedería el máximo permitido.");
        }

        var resulting = OnHand + quantity;
        var movement = new InventoryMovement(ProductId, MovementCount + 1, MovementType.SaleCancellation, quantity, resulting, null, reference);
        OnHandThousandths = resulting.Thousandths;
        MovementCount++;
        return movement;
    }

    private static void ValidateSaleQuantity(Quantity quantity, UnitOfMeasure unit, string reference)
    {
        if (quantity <= Quantity.Zero || quantity.Thousandths > Quantity.MaxCaptureThousandths)
        {
            throw new DomainException("La cantidad debe ser mayor que 0 y no exceder el máximo permitido.");
        }

        if (!quantity.FitsDecimals(unit.DecimalPlaces))
        {
            throw new DomainException($"La cantidad excede los decimales que admite la unidad {unit.Name}.");
        }

        if (InventoryMovement.NormalizeText(reference) is { Length: > InventoryMovement.ReferenceMaxLength })
        {
            throw new DomainException("La referencia excede la longitud permitida.");
        }
    }
}
