using Pos.Domain.Sales;

namespace Pos.Application.Sales.ConfirmSale;

/// <summary>Una línea de la venta: producto, cantidad y el precio que vio el operador.</summary>
public sealed record ConfirmLineInput(Guid ProductId, long QuantityThousandths, long ExpectedUnitPriceCents);

/// <summary>
/// Un pago. Para efectivo, <see cref="AmountCents"/> se ignora: lo recalcula <c>Checkout</c> a
/// partir de lo recibido.
/// </summary>
public sealed record PaymentInput(PaymentMethod Method, long AmountCents, long? ReceivedCents, string? Reference);

/// <summary>
/// Venta por confirmar. Una venta a crédito (014) lleva un único pago <c>ACCOUNT</c> por el total y el
/// <c>CustomerId</c>; <c>OverLimitGrantId</c> es la concesión de <c>ApproveCreditOverLimit</c> cuando
/// la venta excede el límite del cliente.
/// </summary>
public sealed record ConfirmSaleCommand(
    Guid DraftId,
    IReadOnlyList<ConfirmLineInput> Lines,
    IReadOnlyList<PaymentInput> Payments,
    Guid? CustomerId = null,
    Guid? OverLimitGrantId = null);
