using Pos.Domain.Common;

namespace Pos.Domain.CreditNotes;

/// <summary>Movimiento del saldo de una nota de crédito.</summary>
public enum CreditNoteMovementType
{
    Issue,
    Redeem,
    Restore,
}

public static class CreditNoteMovementTypeExtensions
{
    public static string ToCode(this CreditNoteMovementType type) => type switch
    {
        CreditNoteMovementType.Issue => "ISSUE",
        CreditNoteMovementType.Redeem => "REDEEM",
        CreditNoteMovementType.Restore => "RESTORE",
        _ => throw new DomainException("El tipo de movimiento de la nota no es válido."),
    };

    public static CreditNoteMovementType FromCode(string code) => code switch
    {
        "ISSUE" => CreditNoteMovementType.Issue,
        "REDEEM" => CreditNoteMovementType.Redeem,
        "RESTORE" => CreditNoteMovementType.Restore,
        _ => throw new DomainException("El tipo de movimiento de la nota no es válido."),
    };
}
