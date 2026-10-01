using Pos.Domain.Discounts;

namespace Pos.Application.Discounts.Coupons.SaveCoupon;

/// <summary>
/// Alta (<c>Id</c> nulo) o edición de un cupón (015, FR-009). <c>ValueText</c> es lo capturado: porcentaje
/// ("12.5") o monto ("15.00"). <c>UsageLimit</c> nulo = sin límite. <c>ExpectedVersion</c> solo en la edición.
/// </summary>
public sealed record SaveCouponCommand(
    Guid? Id,
    string Code,
    DiscountMode Mode,
    string ValueText,
    DateOnly StartsOn,
    DateOnly EndsOn,
    int? UsageLimit,
    int ExpectedVersion = 0);
