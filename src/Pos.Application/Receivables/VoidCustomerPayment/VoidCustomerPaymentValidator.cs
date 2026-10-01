using FluentValidation;
using Pos.Domain.Receivables;

namespace Pos.Application.Receivables.VoidCustomerPayment;

public sealed class VoidCustomerPaymentValidator : AbstractValidator<VoidCustomerPaymentCommand>
{
    public VoidCustomerPaymentValidator() =>
        RuleFor(c => c.Reason)
            .Cascade(CascadeMode.Stop)
            .Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage(ReceivableMessages.ReasonRequired)
            .Must(r => r.Trim().Length <= CustomerPayment.VoidReasonMaxLength).WithMessage(ReceivableMessages.ReasonTooLong)
            .OverridePropertyName(ReceivableFields.Reason);
}
