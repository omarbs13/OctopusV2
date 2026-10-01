using Pos.Domain.Common;
using Pos.Domain.Sales;

namespace Pos.Domain.Receivables;

/// <summary>
/// Abono de un cliente (014, research §5). Es inmutable salvo una transición: <c>ACTIVE → VOIDED</c>,
/// una sola vez (<see cref="Void"/>). Su reparto entre las cuentas vive en el libro
/// <see cref="ReceivableEntry"/>. <c>CreatedAt</c> y <c>CreatedBy</c> los asigna la persistencia.
/// </summary>
public sealed class CustomerPayment
{
    public const int ReferenceMaxLength = 50;
    public const int VoidReasonMaxLength = 250;

    private CustomerPayment()
    {
    }

    public Guid Id { get; private set; }

    public long Number { get; private set; }

    /// <summary>Clave de idempotencia que genera la interfaz al abrir el formulario.</summary>
    public Guid RequestId { get; private set; }

    public Guid CustomerId { get; private set; }

    public long AmountCents { get; private set; }

    public PaymentMethod Method { get; private set; }

    public string? Reference { get; private set; }

    /// <summary>Turno en que se registró; nulo solo si el módulo Turnos no tiene licencia.</summary>
    public Guid? CashShiftId { get; private set; }

    public long BalanceBeforeCents { get; private set; }

    public long BalanceAfterCents { get; private set; }

    public CustomerPaymentStatus Status { get; private set; }

    public DateTime? VoidedAt { get; private set; }

    public Guid? VoidedBy { get; private set; }

    public Guid? VoidAuthorizedBy { get; private set; }

    public string? VoidReason { get; private set; }

    /// <summary>Turno en que se anuló (corte y efectivo, research §6).</summary>
    public Guid? VoidCashShiftId { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public string Folio => CustomerPaymentFolio.Format(Number);

    /// <summary>Formas de pago admitidas en un abono (FR-009): ni nota de crédito ni crédito.</summary>
    public static bool IsAllowedMethod(PaymentMethod method) =>
        method is PaymentMethod.Cash or PaymentMethod.Card or PaymentMethod.Transfer;

    public static CustomerPayment Register(
        long number,
        Guid requestId,
        Guid customerId,
        long amountCents,
        PaymentMethod method,
        string? reference,
        Guid? cashShiftId,
        long balanceBeforeCents)
    {
        if (number < 1 || requestId == Guid.Empty || customerId == Guid.Empty || cashShiftId == Guid.Empty)
        {
            throw new DomainException("El abono no es válido.");
        }

        if (amountCents <= 0 || amountCents > balanceBeforeCents)
        {
            throw new DomainException("El monto debe ser mayor que 0 y no exceder el saldo del cliente.");
        }

        if (!IsAllowedMethod(method))
        {
            throw new DomainException("La forma de pago del abono debe ser efectivo, tarjeta o transferencia.");
        }

        var text = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        if (text is { Length: > ReferenceMaxLength })
        {
            throw new DomainException($"La referencia admite hasta {ReferenceMaxLength} caracteres.");
        }

        return new CustomerPayment
        {
            Id = Guid.CreateVersion7(),
            Number = number,
            RequestId = requestId,
            CustomerId = customerId,
            AmountCents = amountCents,
            Method = method,
            Reference = text,
            CashShiftId = cashShiftId,
            BalanceBeforeCents = balanceBeforeCents,
            BalanceAfterCents = balanceBeforeCents - amountCents,
            Status = CustomerPaymentStatus.Active,
        };
    }

    /// <summary>Única mutación del abono: lo anula una sola vez, con motivo obligatorio (FR-014).</summary>
    /// <exception cref="InvalidOperationException">El abono ya está anulado o falta el motivo.</exception>
    public void Void(string reason, Guid userId, Guid authorizedBy, Guid? shiftId, DateTime utcNow)
    {
        if (Status != CustomerPaymentStatus.Active)
        {
            throw new InvalidOperationException("El abono ya está anulado.");
        }

        var text = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (text is null or { Length: > VoidReasonMaxLength })
        {
            throw new InvalidOperationException($"El motivo es obligatorio y admite hasta {VoidReasonMaxLength} caracteres.");
        }

        Status = CustomerPaymentStatus.Voided;
        VoidReason = text;
        VoidedBy = userId;
        VoidAuthorizedBy = authorizedBy;
        VoidCashShiftId = shiftId;
        VoidedAt = utcNow;
    }
}
