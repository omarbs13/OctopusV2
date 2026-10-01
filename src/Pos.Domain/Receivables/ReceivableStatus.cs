using Pos.Domain.Common;

namespace Pos.Domain.Receivables;

/// <summary>Estado de una cuenta por cobrar (014): <c>PENDING ⇄ PAID</c>; <c>CANCELLED</c> es terminal.</summary>
public enum ReceivableStatus
{
    Pending,
    Paid,
    Cancelled,
}

public static class ReceivableStatusExtensions
{
    public static string ToCode(this ReceivableStatus status) => status switch
    {
        ReceivableStatus.Pending => "PENDING",
        ReceivableStatus.Paid => "PAID",
        ReceivableStatus.Cancelled => "CANCELLED",
        _ => throw new DomainException("El estado de la cuenta por cobrar no es válido."),
    };

    public static ReceivableStatus FromCode(string code) => code switch
    {
        "PENDING" => ReceivableStatus.Pending,
        "PAID" => ReceivableStatus.Paid,
        "CANCELLED" => ReceivableStatus.Cancelled,
        _ => throw new DomainException("El estado de la cuenta por cobrar no es válido."),
    };
}
