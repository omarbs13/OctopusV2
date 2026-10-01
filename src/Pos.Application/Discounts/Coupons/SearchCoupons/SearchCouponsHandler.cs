using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Discounts.Coupons.SearchCoupons;

/// <summary>Cupones con vigencia, estado de hoy, usos realizados y restantes; solo <c>ManageDiscounts</c>.</summary>
public sealed class SearchCouponsHandler
{
    private readonly IAccessControl _access;
    private readonly ICouponRepository _coupons;
    private readonly IClock _clock;

    public SearchCouponsHandler(IAccessControl access, ICouponRepository coupons, IClock clock)
    {
        _access = access;
        _coupons = coupons;
        _clock = clock;
    }

    public async Task<Result<CouponPage>> HandleAsync(SearchCouponsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var access = await _access.CheckAsync(Permission.ManageDiscounts, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<CouponPage>(access.Error!);
        }

        var text = string.IsNullOrWhiteSpace(query.Text) ? null : query.Text.Trim();
        var page = await _coupons.SearchAsync(
            new CouponSearch(text, query.Status, Math.Max(1, query.Page), Math.Clamp(query.PageSize, 1, CouponPage.DefaultPageSize)),
            DiscountDates.LocalToday(_clock),
            cancellationToken);
        return Result.Success(page);
    }
}
