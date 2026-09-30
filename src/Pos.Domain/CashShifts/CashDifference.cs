using Pos.Domain.Common;

namespace Pos.Domain.CashShifts;

public enum DifferenceKind
{
    Balanced,
    Over,
    Shortage,
}

/// <summary>Diferencia del arqueo: monto absoluto y sentido. <c>Money</c> no admite negativos (research §4).</summary>
public readonly record struct CashDifference(Money Amount, DifferenceKind Kind)
{
    public static CashDifference From(long countedCents, long expectedCents) =>
        FromSigned(countedCents - expectedCents);

    public static CashDifference FromSigned(long signedCents)
    {
        var kind = signedCents switch
        {
            0 => DifferenceKind.Balanced,
            > 0 => DifferenceKind.Over,
            _ => DifferenceKind.Shortage,
        };
        return new CashDifference(Money.FromCents(Math.Min(Math.Abs(signedCents), Money.MaxCents)), kind);
    }
}
