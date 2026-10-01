using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Discounts.Coupons.GetCoupon;

/// <summary>Datos de un cupón para editarlo; solo <c>ManageDiscounts</c>.</summary>
public sealed class GetCouponHandler
{
    private readonly IAccessControl _access;
    private readonly ICouponRepository _coupons;
    private readonly IClock _clock;

    public GetCouponHandler(IAccessControl access, ICouponRepository coupons, IClock clock)
    {
        _access = access;
        _coupons = coupons;
        _clock = clock;
    }

    public async Task<Result<CouponDetailDto>> HandleAsync(GetCouponQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var access = await _access.CheckAsync(Permission.ManageDiscounts, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<CouponDetailDto>(access.Error!);
        }

        var coupon = await _coupons.GetAsync(query.Id, cancellationToken);
        return coupon is null
            ? Result.Failure<CouponDetailDto>(new NotFound())
            : Result.Success(new CouponDetailDto(
                coupon.Id,
                coupon.Code,
                coupon.Mode,
                coupon.Value,
                coupon.StartsOn,
                coupon.EndsOn,
                coupon.UsageLimit,
                coupon.UsesCount,
                coupon.IsActive,
                coupon.StatusOn(DiscountDates.LocalToday(_clock)),
                coupon.Version));
    }
}
