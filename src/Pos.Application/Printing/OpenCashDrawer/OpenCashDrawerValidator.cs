using FluentValidation;

namespace Pos.Application.Printing.OpenCashDrawer;

public sealed class OpenCashDrawerValidator : AbstractValidator<OpenCashDrawerCommand>
{
    public OpenCashDrawerValidator() =>
        RuleFor(c => (c.Reason ?? string.Empty).Trim())
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(PrintingMessages.DrawerReasonRequired)
            .MaximumLength(OpenCashDrawerCommand.ReasonMaxLength).WithMessage(PrintingMessages.DrawerReasonTooLong)
            .OverridePropertyName(PrintingFields.Reason)
            .When(c => c.IsManual);
}
