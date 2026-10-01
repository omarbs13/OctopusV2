using FluentValidation;

namespace Pos.Application.Receivables.SaveReceivablesSettings;

public sealed class SaveReceivablesSettingsValidator : AbstractValidator<SaveReceivablesSettingsCommand>
{
    public SaveReceivablesSettingsValidator() =>
        RuleFor(c => c.PaymentTermDays)
            .InclusiveBetween(ReceivablesSettings.MinPaymentTermDays, ReceivablesSettings.MaxPaymentTermDays)
            .WithMessage(ReceivableMessages.TermRange)
            .OverridePropertyName(ReceivableFields.PaymentTermDays);
}
