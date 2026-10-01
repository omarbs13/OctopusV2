using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Discounts;
using Pos.Domain.Users;

namespace Pos.Application.Discounts.ResolveCoupon;

/// <summary>
/// Busca un cupón por su código para "Aplicar cupón" (015, FR-011): nulo si no existe; si existe, su estado
/// de hoy (fecha local). La interfaz traduce un estado distinto de vigente en el mensaje de la causa.
/// </summary>
public sealed class ResolveCouponHandler
{
    private readonly IAccessControl _access;
    private readonly ICouponRepository _coupons;
    private readonly IClock _clock;

    public ResolveCouponHandler(IAccessControl access, ICouponRepository coupons, IClock clock)
    {
        _access = access;
        _coupons = coupons;
        _clock = clock;
    }

    public async Task<Result<CouponLookupDto?>> HandleAsync(ResolveCouponQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var access = await _access.CheckAsync(Permission.ApplyDiscounts, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<CouponLookupDto?>(access.Error!);
        }

        var code = Coupon.NormalizeCode(query.Code);
        if (code.Length == 0)
        {
            return Result.Failure<CouponLookupDto?>(new ValidationFailed([new FieldError(DiscountFields.Code, DiscountMessages.CodeRequired)]));
        }

        var coupon = Coupon.IsValidCode(code) ? await _coupons.FindByCodeAsync(code, cancellationToken) : null;
        return Result.Success(coupon is null ? null : ToLookup(coupon, DiscountDates.LocalToday(_clock)));
    }

    public static CouponLookupDto ToLookup(Coupon coupon, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(coupon);
        return new CouponLookupDto(coupon.Id, coupon.Code, coupon.Mode, coupon.Value, coupon.StatusOn(today), coupon.StartsOn, coupon.EndsOn);
    }
}
