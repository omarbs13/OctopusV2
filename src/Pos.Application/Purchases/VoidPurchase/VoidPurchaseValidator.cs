using FluentValidation;
using Pos.Domain.Purchases;

namespace Pos.Application.Purchases.VoidPurchase;

public sealed class VoidPurchaseValidator : AbstractValidator<VoidPurchaseCommand>
{
    public VoidPurchaseValidator() =>
        RuleFor(c => c.Reason)
            .Cascade(CascadeMode.Stop)
            .Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage(PurchaseMessages.ReasonRequired)
            .Must(r => r.Trim().Length <= Purchase.VoidReasonMaxLength).WithMessage(PurchaseMessages.ReasonTooLong)
            .OverridePropertyName(PurchaseFields.Reason);
}
