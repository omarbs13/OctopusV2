using FluentValidation;
using Pos.Domain.Discounts;

namespace Pos.Application.Discounts.Coupons.SaveCoupon;

/// <summary>Forma de la entrada; las reglas de negocio las aplica <see cref="Coupon"/>.</summary>
public sealed class SaveCouponValidator : AbstractValidator<SaveCouponCommand>
{
    public SaveCouponValidator()
    {
        RuleFor(c => c.Code)
            .Cascade(CascadeMode.Stop)
            .Must(code => !string.IsNullOrWhiteSpace(code)).WithMessage(DiscountMessages.CodeRequired)
            .Must(Coupon.IsValidCode).WithMessage(DiscountMessages.CodeFormat)
            .OverridePropertyName(DiscountFields.Code);
        RuleFor(c => c.Mode).IsInEnum().WithMessage(DiscountMessages.ModeInvalid).OverridePropertyName(DiscountFields.Mode);
        RuleFor(c => c.EndsOn)
            .Must((c, endsOn) => endsOn >= c.StartsOn).WithMessage(DiscountMessages.DatesInvalid)
            .OverridePropertyName(DiscountFields.EndsOn);
        RuleFor(c => c.UsageLimit)
            .Must(limit => limit is null or > 0).WithMessage(DiscountMessages.UsageLimitInvalid)
            .OverridePropertyName(DiscountFields.UsageLimit);
    }
}
