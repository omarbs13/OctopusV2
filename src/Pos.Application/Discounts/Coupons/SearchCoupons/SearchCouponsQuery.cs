using Pos.Domain.Discounts;

namespace Pos.Application.Discounts.Coupons.SearchCoupons;

/// <summary>Listado de cupones por código y estado, paginado de 100 (FR-009).</summary>
public sealed record SearchCouponsQuery(string? Text, CouponStatus? Status, int Page = 1, int PageSize = CouponPage.DefaultPageSize);
