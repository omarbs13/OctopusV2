using FluentValidation;

namespace Pos.Application.Discounts.Settings.SaveDiscountSettings;

public sealed class SaveDiscountSettingsValidator : AbstractValidator<SaveDiscountSettingsCommand>
{
    public SaveDiscountSettingsValidator() =>
        RuleFor(c => c.LimitBasisPoints)
            .InclusiveBetween(DiscountSettings.MinLimitBasisPoints, DiscountSettings.MaxLimitBasisPoints)
            .WithMessage(DiscountMessages.LimitRange)
            .OverridePropertyName(DiscountFields.Limit);
}
