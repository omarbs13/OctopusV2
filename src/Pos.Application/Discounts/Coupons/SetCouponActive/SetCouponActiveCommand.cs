namespace Pos.Application.Discounts.Coupons.SetCouponActive;

public sealed record SetCouponActiveCommand(Guid Id, bool Active, int ExpectedVersion);
