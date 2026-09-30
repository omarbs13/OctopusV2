using Pos.Domain.Common;

namespace Pos.Domain.CashShifts;

/// <summary>Ingreso (más efectivo en caja) o retiro (menos efectivo en caja).</summary>
public enum CashMovementType
{
    In,
    Out,
}

public static class CashMovementTypeExtensions
{
    public static string ToCode(this CashMovementType type) => type switch
    {
        CashMovementType.In => "IN",
        CashMovementType.Out => "OUT",
        _ => throw new DomainException("El tipo de movimiento de efectivo no es válido."),
    };

    public static CashMovementType FromCode(string code) => code switch
    {
        "IN" => CashMovementType.In,
        "OUT" => CashMovementType.Out,
        _ => throw new DomainException("El tipo de movimiento de efectivo no es válido."),
    };
}
