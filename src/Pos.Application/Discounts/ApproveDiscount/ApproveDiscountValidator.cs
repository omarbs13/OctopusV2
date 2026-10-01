using FluentValidation;
using Pos.Domain.Common;
using Pos.Domain.Discounts;

namespace Pos.Application.Discounts.ApproveDiscount;

public sealed class ApproveDiscountValidator : AbstractValidator<ApproveDiscountCommand>
{
    public ApproveDiscountValidator()
    {
        RuleFor(c => c.DraftId).NotEqual(Guid.Empty).WithMessage(DiscountMessages.ModeInvalid).OverridePropertyName(DiscountFields.Discount);
        RuleFor(c => c.Mode).IsInEnum().WithMessage(DiscountMessages.ModeInvalid).OverridePropertyName(DiscountFields.Mode);
        RuleFor(c => c.ProductId)
            .Must((c, id) => c.Scope == DiscountScope.Line ? id is { } productId && productId != Guid.Empty : id is null)
            .WithMessage(DiscountMessages.ModeInvalid)
            .OverridePropertyName(DiscountFields.Discount);
        RuleFor(c => c.BaseCents).InclusiveBetween(1, Money.MaxCents).WithMessage(DiscountMessages.ModeInvalid)
            .OverridePropertyName(DiscountFields.Value);
    }
}
