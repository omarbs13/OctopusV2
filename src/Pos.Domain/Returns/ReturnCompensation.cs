using Pos.Domain.Common;

namespace Pos.Domain.Returns;

/// <summary>Cómo se compensa al cliente: reintegro del dinero o nota de crédito.</summary>
public enum ReturnCompensation
{
    Refund,
    CreditNote,
}

public static class ReturnCompensationExtensions
{
    public static string ToCode(this ReturnCompensation compensation) => compensation switch
    {
        ReturnCompensation.Refund => "REFUND",
        ReturnCompensation.CreditNote => "CREDIT_NOTE",
        _ => throw new DomainException("La compensación no es válida."),
    };

    public static ReturnCompensation FromCode(string code) => code switch
    {
        "REFUND" => ReturnCompensation.Refund,
        "CREDIT_NOTE" => ReturnCompensation.CreditNote,
        _ => throw new DomainException("La compensación no es válida."),
    };
}
