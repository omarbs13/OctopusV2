using Pos.Domain.Common;

namespace Pos.Domain.CashShifts;

public enum CashShiftStatus
{
    Open,
    Closed,
}

public static class CashShiftStatusExtensions
{
    public static string ToCode(this CashShiftStatus status) => status switch
    {
        CashShiftStatus.Open => "OPEN",
        CashShiftStatus.Closed => "CLOSED",
        _ => throw new DomainException("El estado del turno no es válido."),
    };

    public static CashShiftStatus FromCode(string code) => code switch
    {
        "OPEN" => CashShiftStatus.Open,
        "CLOSED" => CashShiftStatus.Closed,
        _ => throw new DomainException("El estado del turno no es válido."),
    };
}
