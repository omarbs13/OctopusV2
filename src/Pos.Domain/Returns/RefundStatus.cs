using Pos.Domain.Common;

namespace Pos.Domain.Returns;

/// <summary>Estado de un reintegro por forma de pago.</summary>
public enum RefundStatus
{
    /// <summary>Efectivo entregado desde la caja.</summary>
    Paid,

    /// <summary>Tarjeta o transferencia anotada para reversa manual.</summary>
    PendingReversal,

    /// <summary>Reversa manual ya realizada.</summary>
    Reversed,

    /// <summary>Monto devuelto al saldo de la nota de crédito con que se pagó.</summary>
    Restored,
}

public static class RefundStatusExtensions
{
    public static string ToCode(this RefundStatus status) => status switch
    {
        RefundStatus.Paid => "PAID",
        RefundStatus.PendingReversal => "PENDING_REVERSAL",
        RefundStatus.Reversed => "REVERSED",
        RefundStatus.Restored => "RESTORED",
        _ => throw new DomainException("El estado del reintegro no es válido."),
    };

    public static RefundStatus FromCode(string code) => code switch
    {
        "PAID" => RefundStatus.Paid,
        "PENDING_REVERSAL" => RefundStatus.PendingReversal,
        "REVERSED" => RefundStatus.Reversed,
        "RESTORED" => RefundStatus.Restored,
        _ => throw new DomainException("El estado del reintegro no es válido."),
    };
}
