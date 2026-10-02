using Pos.Domain.Common;

namespace Pos.Domain.Suppliers;

/// <summary>Condiciones de pago de un proveedor (020, FR-002).</summary>
public enum PaymentTerms
{
    Cash,
    Credit,
}

public static class PaymentTermsExtensions
{
    public static string ToCode(this PaymentTerms terms) => terms switch
    {
        PaymentTerms.Cash => "CASH",
        PaymentTerms.Credit => "CREDIT",
        _ => throw new DomainException("Las condiciones de pago no son válidas."),
    };

    public static PaymentTerms FromCode(string code) => code switch
    {
        "CASH" => PaymentTerms.Cash,
        "CREDIT" => PaymentTerms.Credit,
        _ => throw new DomainException("Las condiciones de pago no son válidas."),
    };
}
