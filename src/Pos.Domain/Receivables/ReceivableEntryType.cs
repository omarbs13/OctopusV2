using Pos.Domain.Common;

namespace Pos.Domain.Receivables;

/// <summary>Tipo de movimiento del libro de una cuenta por cobrar (014, data-model "Tipos de entrada").</summary>
public enum ReceivableEntryType
{
    /// <summary>Parte de un abono aplicada a la cuenta (−).</summary>
    Payment,

    /// <summary>Reverso exacto de un abono anulado (+).</summary>
    PaymentVoid,

    /// <summary>Monto devuelto o cancelado de la venta (−).</summary>
    Return,

    /// <summary>Lo abonado de más sobre lo devuelto, que se libera de la cuenta (+).</summary>
    ExcessOut,

    /// <summary>Ese excedente aplicado a otra cuenta pendiente del cliente (−).</summary>
    ExcessIn,
}

public static class ReceivableEntryTypeExtensions
{
    public static string ToCode(this ReceivableEntryType type) => type switch
    {
        ReceivableEntryType.Payment => "PAYMENT",
        ReceivableEntryType.PaymentVoid => "PAYMENT_VOID",
        ReceivableEntryType.Return => "RETURN",
        ReceivableEntryType.ExcessOut => "EXCESS_OUT",
        ReceivableEntryType.ExcessIn => "EXCESS_IN",
        _ => throw new DomainException("El tipo de movimiento de la cuenta no es válido."),
    };

    public static ReceivableEntryType FromCode(string code) => code switch
    {
        "PAYMENT" => ReceivableEntryType.Payment,
        "PAYMENT_VOID" => ReceivableEntryType.PaymentVoid,
        "RETURN" => ReceivableEntryType.Return,
        "EXCESS_OUT" => ReceivableEntryType.ExcessOut,
        "EXCESS_IN" => ReceivableEntryType.ExcessIn,
        _ => throw new DomainException("El tipo de movimiento de la cuenta no es válido."),
    };
}
