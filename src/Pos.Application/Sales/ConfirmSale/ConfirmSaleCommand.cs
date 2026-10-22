using Pos.Domain.Discounts;
using Pos.Domain.Sales;

namespace Pos.Application.Sales.ConfirmSale;

/// <summary>Descuento de una línea (015): valor capturado y la aprobación de <c>ApproveDiscount</c>, si superó el límite.</summary>
public sealed record LineDiscountInput(DiscountMode Mode, long Value, Guid? ApprovalId = null);

/// <summary>
/// Descuento de la venta (015): manual (modalidad, valor y aprobación) o cupón (<see cref="CouponCode"/>).
/// Son excluyentes (FR-013).
/// </summary>
public sealed record OrderDiscountInput(DiscountMode Mode, long Value, Guid? ApprovalId = null, string? CouponCode = null)
{
    public bool IsCoupon => CouponCode is not null;

    public static OrderDiscountInput Manual(DiscountMode mode, long value, Guid? approvalId = null) => new(mode, value, approvalId);

    public static OrderDiscountInput Coupon(string code) => new(DiscountMode.Percent, 0, null, code);
}

/// <summary>Una línea de la venta: producto, cantidad, el precio que vio el operador y su descuento (015).</summary>
public sealed record ConfirmLineInput(Guid ProductId, long QuantityThousandths, long ExpectedUnitPriceCents, LineDiscountInput? Discount = null);

/// <summary>
/// Un pago. Para efectivo, <see cref="AmountCents"/> se ignora: lo recalcula <c>Checkout</c> a
/// partir de lo recibido.
/// </summary>
public sealed record PaymentInput(PaymentMethod Method, long AmountCents, long? ReceivedCents, string? Reference);

/// <summary>
/// Venta por confirmar. Una venta a crédito (014) lleva un único pago <c>ACCOUNT</c> por el total y el
/// <c>CustomerId</c>; <c>OverLimitGrantId</c> es la concesión de <c>ApproveCreditOverLimit</c> cuando
/// la venta excede el límite del cliente. Una venta de total 0 (015) no lleva pagos.
/// </summary>
public sealed record ConfirmSaleCommand(
    Guid DraftId,
    IReadOnlyList<ConfirmLineInput> Lines,
    IReadOnlyList<PaymentInput> Payments,
    Guid? CustomerId = null,
    Guid? OverLimitGrantId = null,
    OrderDiscountInput? OrderDiscount = null)
{
    public bool HasDiscounts => OrderDiscount is not null || Lines.Any(l => l.Discount is not null);
}
