using FluentValidation;
using Pos.Domain.CashShifts;
using Pos.Domain.Common;

namespace Pos.Application.CashShifts.RegisterCashMovement;

public sealed class RegisterCashMovementValidator : AbstractValidator<RegisterCashMovementCommand>
{
    public RegisterCashMovementValidator()
    {
        RuleFor(c => c.AmountCents)
            .Cascade(CascadeMode.Stop)
            .GreaterThan(0).WithMessage(CashShiftMessages.AmountInvalid)
            .LessThanOrEqualTo(Money.MaxCents).WithMessage(CashShiftMessages.AmountTooLarge)
            .OverridePropertyName(CashShiftFields.Amount);

        RuleFor(c => (c.Reason ?? string.Empty).Trim())
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(CashShiftMessages.ReasonRequired)
            .MaximumLength(CashMovement.ReasonMaxLength).WithMessage(CashShiftMessages.ReasonTooLong)
            .OverridePropertyName(CashShiftFields.Reason);
    }
}
