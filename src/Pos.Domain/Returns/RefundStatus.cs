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

    /// <summary>Parte de una devolución de venta a crédito que redujo la deuda del cliente (014, research §8).</summary>
    Settled,
}

public static class RefundStatusExtensions
{
    public static string ToCode(this RefundStatus status) => status switch
    {
        RefundStatus.Paid => "PAID",
        RefundStatus.PendingReversal => "PENDING_REVERSAL",
        RefundStatus.Reversed => "REVERSED",
        RefundStatus.Restored => "RESTORED",
        RefundStatus.Settled => "SETTLED",
        _ => throw new DomainException("El estado del reintegro no es válido."),
    };

    public static RefundStatus FromCode(string code) => code switch
    {
        "PAID" => RefundStatus.Paid,
        "PENDING_REVERSAL" => RefundStatus.PendingReversal,
        "REVERSED" => RefundStatus.Reversed,
        "RESTORED" => RefundStatus.Restored,
        "SETTLED" => RefundStatus.Settled,
        _ => throw new DomainException("El estado del reintegro no es válido."),
    };
}
