using FluentValidation;

namespace Pos.Application.Returns.SaveReturnsSettings;

public sealed class SaveReturnsSettingsValidator : AbstractValidator<SaveReturnsSettingsCommand>
{
    public SaveReturnsSettingsValidator() =>
        RuleFor(c => c.ReturnWindowDays)
            .InclusiveBetween(ReturnsSettings.MinReturnWindowDays, ReturnsSettings.MaxReturnWindowDays)
            .WithMessage(ReturnMessages.WindowRange)
            .OverridePropertyName(ReturnFields.ReturnWindowDays);
}
