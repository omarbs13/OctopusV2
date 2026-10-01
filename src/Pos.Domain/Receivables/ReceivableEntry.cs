using Pos.Domain.Common;

namespace Pos.Domain.Receivables;

/// <summary>
/// Renglón inmutable del libro de una cuenta por cobrar (014, research §3). Solo lo crea
/// <see cref="Receivable"/>. <c>AmountCents</c> lleva signo: negativo baja el saldo y positivo lo sube.
/// <c>CreatedAt</c> y <c>CreatedBy</c> los asigna la persistencia.
/// </summary>
public sealed class ReceivableEntry
{
    private ReceivableEntry()
    {
    }

    internal ReceivableEntry(
        Guid receivableId,
        ReceivableEntryType type,
        long amountCents,
        Guid? customerPaymentId,
        Guid? saleReturnId)
    {
        if (amountCents == 0)
        {
            throw new DomainException("El movimiento de la cuenta no puede ser 0.");
        }

        var byPayment = type is ReceivableEntryType.Payment or ReceivableEntryType.PaymentVoid;
        if (byPayment ? customerPaymentId is null || customerPaymentId == Guid.Empty : saleReturnId is null || saleReturnId == Guid.Empty)
        {
            throw new DomainException("El movimiento de la cuenta debe indicar su origen.");
        }

        Id = Guid.CreateVersion7();
        ReceivableId = receivableId;
        Type = type;
        AmountCents = amountCents;
        CustomerPaymentId = byPayment ? customerPaymentId : null;
        SaleReturnId = byPayment ? null : saleReturnId;
    }

    public Guid Id { get; private set; }

    public Guid ReceivableId { get; private set; }

    public ReceivableEntryType Type { get; private set; }

    public long AmountCents { get; private set; }

    public Guid? CustomerPaymentId { get; private set; }

    public Guid? SaleReturnId { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }
}
