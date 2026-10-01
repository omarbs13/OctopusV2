using Pos.Domain.Common;

namespace Pos.Domain.Sales;

/// <summary>Pago de una venta registrada. Es parte del agregado <see cref="Sale"/>.</summary>
public sealed class SalePayment
{
    public const int ReferenceMaxLength = 50;

    private SalePayment()
    {
    }

    public Guid Id { get; private set; }

    public Guid SaleId { get; private set; }

    public PaymentMethod Method { get; private set; }

    /// <summary>Monto aplicado a la venta, mayor que 0.</summary>
    public long AmountCents { get; private set; }

    public long? ReceivedCents { get; private set; }

    public long? ChangeCents { get; private set; }

    public string? Reference { get; private set; }

    /// <summary>Nota de crédito usada; solo con <see cref="PaymentMethod.CreditNote"/> (sin llave foránea).</summary>
    public Guid? CreditNoteId { get; private set; }

    public static SalePayment Create(PaymentEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Amount.Cents <= 0)
        {
            throw new DomainException("El monto del pago debe ser mayor que 0.");
        }

        var isCash = entry.Method == PaymentMethod.Cash;
        if (isCash && (entry.Received is null || entry.Change is null || entry.Reference is not null))
        {
            throw new DomainException("El pago en efectivo requiere monto recibido y cambio.");
        }

        if (!isCash && (entry.Received is not null || entry.Change is not null))
        {
            throw new DomainException("Solo el pago en efectivo tiene monto recibido y cambio.");
        }

        var reference = string.IsNullOrWhiteSpace(entry.Reference) ? null : entry.Reference.Trim();
        if (reference is { Length: > ReferenceMaxLength })
        {
            throw new DomainException($"La referencia admite hasta {ReferenceMaxLength} caracteres.");
        }

        if (entry.Method == PaymentMethod.CreditNote && reference is null)
        {
            throw new DomainException("El pago con nota de crédito requiere el folio.");
        }

        return new SalePayment
        {
            Id = Guid.CreateVersion7(),
            Method = entry.Method,
            AmountCents = entry.Amount.Cents,
            ReceivedCents = entry.Received?.Cents,
            ChangeCents = entry.Change?.Cents,
            Reference = reference,
        };
    }

    /// <summary>Liga el pago con la nota de crédito usada.</summary>
    public void LinkCreditNote(Guid creditNoteId)
    {
        if (Method != PaymentMethod.CreditNote || creditNoteId == Guid.Empty)
        {
            throw new DomainException("Solo un pago con nota de crédito puede ligarse a una nota.");
        }

        CreditNoteId = creditNoteId;
    }
}
