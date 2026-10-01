using Pos.Domain.Common;

namespace Pos.Domain.Discounts;

/// <summary>Alcance de un descuento manual que necesita aprobación: una línea o la venta completa.</summary>
public enum DiscountScope
{
    Line,
    Order,
}

public static class DiscountScopeExtensions
{
    public static string ToCode(this DiscountScope scope) => scope switch
    {
        DiscountScope.Line => "LINE",
        DiscountScope.Order => "ORDER",
        _ => throw new DomainException("El alcance del descuento no es válido."),
    };

    public static DiscountScope FromCode(string code) => code switch
    {
        "LINE" => DiscountScope.Line,
        "ORDER" => DiscountScope.Order,
        _ => throw new DomainException("El alcance del descuento no es válido."),
    };
}
