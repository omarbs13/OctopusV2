using Pos.Domain.Common;

namespace Pos.Domain.Receivables;

/// <summary>Estado de un abono (014): <c>ACTIVE → VOIDED</c>, una sola vez.</summary>
public enum CustomerPaymentStatus
{
    Active,
    Voided,
}

public static class CustomerPaymentStatusExtensions
{
    public static string ToCode(this CustomerPaymentStatus status) => status switch
    {
        CustomerPaymentStatus.Active => "ACTIVE",
        CustomerPaymentStatus.Voided => "VOIDED",
        _ => throw new DomainException("El estado del abono no es válido."),
    };

    public static CustomerPaymentStatus FromCode(string code) => code switch
    {
        "ACTIVE" => CustomerPaymentStatus.Active,
        "VOIDED" => CustomerPaymentStatus.Voided,
        _ => throw new DomainException("El estado del abono no es válido."),
    };
}
