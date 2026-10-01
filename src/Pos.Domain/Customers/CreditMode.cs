using Pos.Domain.Common;

namespace Pos.Domain.Customers;

/// <summary>Modalidad de un cliente: solo efectivo o con crédito disponible (014).</summary>
public enum CreditMode
{
    CashOnly,
    Credit,
}

public static class CreditModeExtensions
{
    public static string ToCode(this CreditMode mode) => mode switch
    {
        CreditMode.CashOnly => "CASH_ONLY",
        CreditMode.Credit => "CREDIT",
        _ => throw new DomainException("La modalidad de crédito no es válida."),
    };

    public static CreditMode FromCode(string code) => code switch
    {
        "CASH_ONLY" => CreditMode.CashOnly,
        "CREDIT" => CreditMode.Credit,
        _ => throw new DomainException("La modalidad de crédito no es válida."),
    };
}
