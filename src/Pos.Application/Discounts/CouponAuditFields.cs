using Pos.Application.Audit;
using Pos.Domain.Discounts;

namespace Pos.Application.Discounts;

/// <summary>Instantánea de auditoría del cupón (018, research §6): los campos editables del formulario.</summary>
public static class CouponAuditFields
{
    public const string State = "Estado";

    public static IReadOnlyList<AuditField> Snapshot(Coupon coupon)
    {
        ArgumentNullException.ThrowIfNull(coupon);
        return
        [
            new("Código", coupon.Code),
            new("Descuento", coupon.Discount.ToString()),
            new("Desde", DiscountMessages.FormatDate(coupon.StartsOn)),
            new("Hasta", DiscountMessages.FormatDate(coupon.EndsOn)),
            new("Límite de usos", coupon.UsageLimit is { } max ? max.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Sin límite"),
            new(State, AuditFormat.ActiveState(coupon.IsActive)),
        ];
    }
}
