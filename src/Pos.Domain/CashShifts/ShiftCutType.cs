using Pos.Domain.Common;

namespace Pos.Domain.CashShifts;

/// <summary>Tipo de corte: lectura parcial del turno abierto (X) o cierre definitivo (Z).</summary>
public enum ShiftCutType
{
    Readout,
    Closing,
}

public static class ShiftCutTypeExtensions
{
    public static string ToCode(this ShiftCutType type) => type switch
    {
        ShiftCutType.Readout => "X",
        ShiftCutType.Closing => "Z",
        _ => throw new DomainException("El tipo de corte no es válido."),
    };

    public static ShiftCutType FromCode(string code) => code switch
    {
        "X" => ShiftCutType.Readout,
        "Z" => ShiftCutType.Closing,
        _ => throw new DomainException("El tipo de corte no es válido."),
    };
}
