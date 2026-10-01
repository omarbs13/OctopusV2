using Pos.Domain.Common;

namespace Pos.Domain.CreditNotes;

/// <summary>
/// Nota de crédito (vale) inmutable: folio e importe inicial. El saldo no se guarda, se calcula con
/// sus <see cref="CreditNoteMovement"/> (research §5). <c>CreatedAt</c> y <c>CreatedBy</c> los asigna la persistencia.
/// </summary>
public sealed class CreditNote
{
    private CreditNote()
    {
    }

    public Guid Id { get; private set; }

    public long Number { get; private set; }

    public long InitialCents { get; private set; }

    public Guid SaleReturnId { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public string Folio => CreditNoteFolio.Format(Number);

    public static CreditNote Issue(long number, Guid saleReturnId, long initialCents)
    {
        if (number < 1 || saleReturnId == Guid.Empty || initialCents <= 0)
        {
            throw new DomainException("La nota de crédito no es válida.");
        }

        return new CreditNote
        {
            Id = Guid.CreateVersion7(),
            Number = number,
            SaleReturnId = saleReturnId,
            InitialCents = initialCents,
        };
    }

    /// <summary>Saldo = Σ emisión + Σ restauración − Σ uso.</summary>
    public static long Balance(long issuedCents, long restoredCents, long redeemedCents) =>
        issuedCents + restoredCents - redeemedCents;

    /// <summary>Movimiento de emisión (consecutivo 1) por el importe inicial.</summary>
    public CreditNoteMovement RecordIssue(Guid saleId) =>
        new(Id, 1, CreditNoteMovementType.Issue, InitialCents, saleId, SaleReturnId);

    /// <summary>Usa saldo como pago: el monto debe ser mayor que 0 y no exceder el saldo actual.</summary>
    public CreditNoteMovement Redeem(long amountCents, long currentBalanceCents, int nextSequence, Guid saleId)
    {
        if (amountCents <= 0)
        {
            throw new DomainException("El monto debe ser mayor que 0.");
        }

        if (amountCents > currentBalanceCents)
        {
            throw new DomainException("El monto excede el saldo de la nota de crédito.");
        }

        return new CreditNoteMovement(Id, nextSequence, CreditNoteMovementType.Redeem, amountCents, saleId, null);
    }

    /// <summary>Regresa saldo a la nota con que se pagó una venta devuelta.</summary>
    public CreditNoteMovement Restore(long amountCents, int nextSequence, Guid saleId, Guid saleReturnId)
    {
        if (amountCents <= 0)
        {
            throw new DomainException("El monto debe ser mayor que 0.");
        }

        return new CreditNoteMovement(Id, nextSequence, CreditNoteMovementType.Restore, amountCents, saleId, saleReturnId);
    }
}
