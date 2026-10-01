using Pos.Domain.Common;

namespace Pos.Domain.CreditNotes;

/// <summary>
/// Renglón inmutable del saldo de una nota de crédito. Solo lo crea <see cref="CreditNote"/>.
/// <c>CreatedAt</c> y <c>CreatedBy</c> los asigna la persistencia.
/// </summary>
public sealed class CreditNoteMovement
{
    private CreditNoteMovement()
    {
    }

    internal CreditNoteMovement(
        Guid creditNoteId,
        int sequence,
        CreditNoteMovementType type,
        long amountCents,
        Guid? saleId,
        Guid? saleReturnId)
    {
        if (sequence < 1 || amountCents <= 0)
        {
            throw new DomainException("El movimiento de la nota de crédito no es válido.");
        }

        Id = Guid.CreateVersion7();
        CreditNoteId = creditNoteId;
        Sequence = sequence;
        Type = type;
        AmountCents = amountCents;
        SaleId = saleId;
        SaleReturnId = saleReturnId;
    }

    public Guid Id { get; private set; }

    public Guid CreditNoteId { get; private set; }

    public int Sequence { get; private set; }

    public CreditNoteMovementType Type { get; private set; }

    public long AmountCents { get; private set; }

    public Guid? SaleId { get; private set; }

    public Guid? SaleReturnId { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }
}
