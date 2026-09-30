using FluentValidation;
using Pos.Domain.Common;

namespace Pos.Application.CashShifts.OpenShift;

public sealed class OpenShiftValidator : AbstractValidator<OpenShiftCommand>
{
    public OpenShiftValidator() =>
        RuleFor(c => c)
            .Cascade(CascadeMode.Stop)
            .Must(c => c.OpeningFloatCents is >= 0 and <= Money.MaxCents)
                .WithMessage(CashShiftMessages.OpeningFloatInvalid)
            .Must(c => c.OpeningFloatCents > 0 || c.ConfirmZeroFloat)
                .WithMessage(CashShiftMessages.OpeningFloatZeroNeedsConfirmation)
            .OverridePropertyName(CashShiftFields.OpeningFloat);
}
