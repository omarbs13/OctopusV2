using Pos.Domain.Discounts;

namespace Pos.Domain.Sales;

/// <summary>
/// Descuento de la venta completa (015): manual o de cupón. Una venta tiene como máximo uno, así que el
/// cupón y el descuento global manual se excluyen (FR-013).
/// </summary>
public abstract record OrderDiscount(DiscountValue Value)
{
    /// <summary>Descuento global capturado por el cajero; <see cref="ApprovalId"/> si superó el límite.</summary>
    public sealed record Manual(DiscountValue Value, Guid? ApprovalId = null) : OrderDiscount(Value);

    /// <summary>Descuento de un cupón vigente; no se compara con el límite (FR-013).</summary>
    public sealed record CouponApplied(Guid CouponId, string Code, DiscountValue Value) : OrderDiscount(Value);
}
