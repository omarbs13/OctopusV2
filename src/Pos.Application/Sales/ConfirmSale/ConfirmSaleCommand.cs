using Pos.Domain.Sales;

namespace Pos.Application.Sales.ConfirmSale;

/// <summary>Una línea de la venta: producto, cantidad y el precio que vio el operador.</summary>
public sealed record ConfirmLineInput(Guid ProductId, long QuantityThousandths, long ExpectedUnitPriceCents);

/// <summary>
/// Un pago. Para efectivo, <see cref="AmountCents"/> se ignora: lo recalcula <c>Checkout</c> a
/// partir de lo recibido.
/// </summary>
public sealed record PaymentInput(PaymentMethod Method, long AmountCents, long? ReceivedCents, string? Reference);

public sealed record ConfirmSaleCommand(
    Guid DraftId,
    IReadOnlyList<ConfirmLineInput> Lines,
    IReadOnlyList<PaymentInput> Payments);
