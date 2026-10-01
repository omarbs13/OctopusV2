using Pos.Domain.Common;
using Pos.Domain.Discounts;

namespace Pos.Domain.Sales;

/// <summary>Motivo por el que una línea de la venta en curso ya no se puede vender.</summary>
public enum UnavailableReason
{
    Inactive,
    Deleted,
}

/// <summary>Producto que se agrega a la venta en curso, con los datos que la venta necesita.</summary>
public sealed record CartProduct(
    Guid ProductId,
    string Name,
    string Sku,
    string UnitCode,
    int DecimalPlaces,
    bool TracksInventory,
    Money UnitPrice,
    UnavailableReason? UnavailableReason = null);

/// <summary>Precio y disponibilidad vigentes de un producto (revisión antes de cobrar).</summary>
public sealed record CartPriceUpdate(Guid ProductId, Money UnitPrice, UnavailableReason? UnavailableReason);

/// <summary>
/// Descuento de una línea de la venta en curso (015): valor capturado y aprobación de un Administrador si
/// superó el límite (research §7).
/// </summary>
public sealed record LineDiscount(DiscountValue Value, Guid? ApprovalId = null);

/// <summary>
/// Línea de la venta en curso. El importe siempre se deduce de cantidad y precio; el descuento (015) se
/// calcula sobre ese importe. La <see cref="Cart"/> garantiza que el descuento siempre sea válido.
/// </summary>
public sealed record CartLine(
    Guid ProductId,
    string Name,
    string Sku,
    string UnitCode,
    int DecimalPlaces,
    bool TracksInventory,
    Money UnitPrice,
    Quantity Quantity,
    UnavailableReason? UnavailableReason = null,
    LineDiscount? Discount = null)
{
    /// <summary>Importe original: cantidad × precio.</summary>
    public Money Amount => SaleMath.LineAmount(Quantity, UnitPrice);

    /// <summary>Descuento propio de la línea; cero si no tiene.</summary>
    public Money LineDiscountAmount => Discount is null
        ? Money.Zero
        : Money.FromCents(DiscountMath.Amount(Amount.Cents, Discount.Value));

    /// <summary>Importe después del descuento de línea: base del subtotal y del reparto del descuento de venta.</summary>
    public Money NetBeforeOrder => Money.FromCents(Amount.Cents - LineDiscountAmount.Cents);

    public bool HasDiscount => Discount is not null;

    public bool IsUnavailable => UnavailableReason is not null;

    /// <summary>Indica si el descuento sigue siendo válido con el importe actual (no excede ni redondea a $0.00).</summary>
    public bool DiscountFits()
    {
        if (Discount is null)
        {
            return true;
        }

        try
        {
            _ = DiscountMath.Amount(Amount.Cents, Discount.Value);
            return true;
        }
        catch (DomainException)
        {
            return false;
        }
    }
}
