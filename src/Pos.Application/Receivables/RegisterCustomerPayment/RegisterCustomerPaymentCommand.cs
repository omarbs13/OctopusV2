using Pos.Domain.Sales;

namespace Pos.Application.Receivables.RegisterCustomerPayment;

/// <summary>
/// Abono de un cliente (014, Historia 3). <c>RequestId</c> lo genera la interfaz al abrir el formulario:
/// un reintento con el mismo valor devuelve el abono ya registrado (doble clic).
/// </summary>
public sealed record RegisterCustomerPaymentCommand(
    Guid RequestId,
    Guid CustomerId,
    long AmountCents,
    PaymentMethod Method,
    string? Reference);
