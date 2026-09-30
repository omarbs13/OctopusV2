using FluentValidation;
using Pos.Domain.Sales;

namespace Pos.Application.Sales.CancelSale;

public sealed class CancelSaleValidator : AbstractValidator<CancelSaleCommand>
{
    public CancelSaleValidator() =>
        RuleFor(c => c.Reason)
            .Cascade(CascadeMode.Stop)
            .Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage(SaleMessages.ReasonRequired)
            .Must(r => r.Trim().Length <= Sale.CancellationReasonMaxLength).WithMessage(SaleMessages.ReasonTooLong)
            .OverridePropertyName(SaleFields.Reason);
}
