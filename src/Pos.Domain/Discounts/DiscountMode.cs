using Pos.Domain.Common;

namespace Pos.Domain.Discounts;

/// <summary>Modalidad de un descuento: porcentaje (en puntos base) o monto fijo (en centavos) (015, research §1).</summary>
public enum DiscountMode
{
    Percent,
    Amount,
}

public static class DiscountModeExtensions
{
    public static string ToCode(this DiscountMode mode) => mode switch
    {
        DiscountMode.Percent => "PERCENT",
        DiscountMode.Amount => "AMOUNT",
        _ => throw new DomainException("La modalidad del descuento no es válida."),
    };

    public static DiscountMode FromCode(string code) => code switch
    {
        "PERCENT" => DiscountMode.Percent,
        "AMOUNT" => DiscountMode.Amount,
        _ => throw new DomainException("La modalidad del descuento no es válida."),
    };
}
