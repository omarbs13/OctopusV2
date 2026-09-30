using Pos.Domain.Common;

namespace Pos.Domain.Sales;

/// <summary>Forma de pago de una venta. Tarjeta y transferencia solo se registran (sin terminal).</summary>
public enum PaymentMethod
{
    Cash,
    Card,
    Transfer,
}

public static class PaymentMethodExtensions
{
    /// <summary>Código de texto estable que se guarda en la base.</summary>
    public static string ToCode(this PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "CASH",
        PaymentMethod.Card => "CARD",
        PaymentMethod.Transfer => "TRANSFER",
        _ => throw new DomainException("La forma de pago no es válida."),
    };

    public static PaymentMethod FromCode(string code) => code switch
    {
        "CASH" => PaymentMethod.Cash,
        "CARD" => PaymentMethod.Card,
        "TRANSFER" => PaymentMethod.Transfer,
        _ => throw new DomainException("La forma de pago no es válida."),
    };
}
