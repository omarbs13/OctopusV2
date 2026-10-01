using Pos.Domain.Common;

namespace Pos.Domain.Sales;

/// <summary>Forma de pago de una venta. Tarjeta y transferencia solo se registran (sin terminal).</summary>
public enum PaymentMethod
{
    Cash,
    Card,
    Transfer,

    /// <summary>Nota de crédito (vale) con saldo; código <c>CREDIT</c> para caber en TEXT(10).</summary>
    CreditNote,

    /// <summary>
    /// Venta a crédito a un cliente (014): único pago de la venta por el total; código <c>ACCOUNT</c>
    /// para no confundirse con la nota de crédito.
    /// </summary>
    OnAccount,
}

public static class PaymentMethodExtensions
{
    /// <summary>Código de texto estable que se guarda en la base.</summary>
    public static string ToCode(this PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "CASH",
        PaymentMethod.Card => "CARD",
        PaymentMethod.Transfer => "TRANSFER",
        PaymentMethod.CreditNote => "CREDIT",
        PaymentMethod.OnAccount => "ACCOUNT",
        _ => throw new DomainException("La forma de pago no es válida."),
    };

    public static PaymentMethod FromCode(string code) => code switch
    {
        "CASH" => PaymentMethod.Cash,
        "CARD" => PaymentMethod.Card,
        "TRANSFER" => PaymentMethod.Transfer,
        "CREDIT" => PaymentMethod.CreditNote,
        "ACCOUNT" => PaymentMethod.OnAccount,
        _ => throw new DomainException("La forma de pago no es válida."),
    };
}
