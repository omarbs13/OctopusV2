using Pos.Domain.Common;

namespace Pos.Domain.Discounts;

/// <summary>Tipo de un descuento registrado en una venta (FR-015).</summary>
public enum DiscountKind
{
    /// <summary>Descuento de una línea.</summary>
    Line,

    /// <summary>Descuento global manual sobre el subtotal.</summary>
    Order,

    /// <summary>Descuento global de un cupón.</summary>
    Coupon,
}

public static class DiscountKindExtensions
{
    public static string ToCode(this DiscountKind kind) => kind switch
    {
        DiscountKind.Line => "LINE",
        DiscountKind.Order => "ORDER",
        DiscountKind.Coupon => "COUPON",
        _ => throw new DomainException("El tipo de descuento no es válido."),
    };

    public static DiscountKind FromCode(string code) => code switch
    {
        "LINE" => DiscountKind.Line,
        "ORDER" => DiscountKind.Order,
        "COUPON" => DiscountKind.Coupon,
        _ => throw new DomainException("El tipo de descuento no es válido."),
    };
}
