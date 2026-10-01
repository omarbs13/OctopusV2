using Pos.Domain.Common;
using Pos.Domain.Sales;

namespace Pos.Domain.Returns;

/// <summary>
/// Reintegro por forma de pago original. Es inmutable salvo una transición:
/// <c>PENDING_REVERSAL → REVERSED</c>, una sola vez (<see cref="MarkReversed"/>).
/// </summary>
public sealed class SaleReturnRefund
{
    private SaleReturnRefund()
    {
    }

    public Guid Id { get; private set; }

    public Guid SaleReturnId { get; private set; }

    public Guid SalePaymentId { get; private set; }

    public PaymentMethod Method { get; private set; }

    public long AmountCents { get; private set; }

    public RefundStatus Status { get; private set; }

    public DateTime? ReversedAt { get; private set; }

    public Guid? ReversedBy { get; private set; }

    /// <summary>Efectivo: pagado; tarjeta y transferencia: pendiente de reversa; nota: restaurada.</summary>
    public static SaleReturnRefund Create(Guid salePaymentId, PaymentMethod method, long amountCents)
    {
        if (salePaymentId == Guid.Empty || amountCents <= 0)
        {
            throw new DomainException("El reintegro no es válido.");
        }

        return new SaleReturnRefund
        {
            Id = Guid.CreateVersion7(),
            SalePaymentId = salePaymentId,
            Method = method,
            AmountCents = amountCents,
            Status = method switch
            {
                PaymentMethod.Cash => RefundStatus.Paid,
                PaymentMethod.CreditNote => RefundStatus.Restored,
                _ => RefundStatus.PendingReversal,
            },
        };
    }

    public void MarkReversed(Guid userId, DateTime utcNow)
    {
        if (Status != RefundStatus.PendingReversal)
        {
            throw new InvalidOperationException("El reintegro no está pendiente de reversa.");
        }

        Status = RefundStatus.Reversed;
        ReversedAt = utcNow;
        ReversedBy = userId;
    }
}
