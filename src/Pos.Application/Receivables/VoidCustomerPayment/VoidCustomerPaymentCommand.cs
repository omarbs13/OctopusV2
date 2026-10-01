namespace Pos.Application.Receivables.VoidCustomerPayment;

/// <summary>
/// Anulación de un abono (014, FR-014). <c>AuthorizationGrantId</c> es la concesión de
/// <c>VoidCustomerPayments</c> de un Administrador, exigida siempre (también al Administrador).
/// </summary>
public sealed record VoidCustomerPaymentCommand(Guid PaymentId, string Reason, Guid? AuthorizationGrantId);
