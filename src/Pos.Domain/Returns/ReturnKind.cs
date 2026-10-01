using Pos.Domain.Common;

namespace Pos.Domain.Returns;

/// <summary>Tipo de devolución: cancelación completa o devolución parcial de líneas.</summary>
public enum ReturnKind
{
    Cancellation,
    Partial,
}

public static class ReturnKindExtensions
{
    public static string ToCode(this ReturnKind kind) => kind switch
    {
        ReturnKind.Cancellation => "CANCELLATION",
        ReturnKind.Partial => "PARTIAL",
        _ => throw new DomainException("El tipo de devolución no es válido."),
    };

    public static ReturnKind FromCode(string code) => code switch
    {
        "CANCELLATION" => ReturnKind.Cancellation,
        "PARTIAL" => ReturnKind.Partial,
        _ => throw new DomainException("El tipo de devolución no es válido."),
    };
}
