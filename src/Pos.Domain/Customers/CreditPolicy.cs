namespace Pos.Domain.Customers;

/// <summary>Resultado de verificar el límite: <c>ExcessCents</c> = 0 si la venta cabe en el límite.</summary>
public readonly record struct CreditCheck(long ExcessCents)
{
    public static CreditCheck Within => default;

    public bool IsWithin => ExcessCents == 0;

    public bool IsExceeded => ExcessCents > 0;

    public static CreditCheck Exceeded(long excessCents) => new(excessCents);
}

/// <summary>Regla del límite de crédito (014, research §4, FR-006): <c>saldo + venta ≤ límite</c>.</summary>
public static class CreditPolicy
{
    public static CreditCheck Check(long balanceCents, long limitCents, long saleCents)
    {
        var after = balanceCents + saleCents;
        return after <= limitCents ? CreditCheck.Within : CreditCheck.Exceeded(after - limitCents);
    }
}
