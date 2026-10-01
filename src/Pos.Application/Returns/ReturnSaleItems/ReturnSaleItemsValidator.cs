using FluentValidation;
using Pos.Domain.Returns;

namespace Pos.Application.Returns.ReturnSaleItems;

public sealed class ReturnSaleItemsValidator : AbstractValidator<ReturnSaleItemsCommand>
{
    public ReturnSaleItemsValidator()
    {
        RuleFor(c => c.Reason)
            .Cascade(CascadeMode.Stop)
            .Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage(ReturnMessages.ReasonRequired)
            .Must(r => r.Trim().Length <= SaleReturn.ReasonMaxLength).WithMessage(ReturnMessages.ReasonTooLong)
            .OverridePropertyName(ReturnFields.Reason);

        RuleFor(c => c.Lines)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(ReturnMessages.LinesRequired)
            .Must(lines => lines.All(l => l.QuantityThousandths > 0)).WithMessage(ReturnMessages.LinesRequired)
            .Must(lines => lines.Select(l => l.SaleLineId).Distinct().Count() == lines.Count).WithMessage(ReturnMessages.LinesRequired)
            .OverridePropertyName(ReturnFields.Lines);

        RuleFor(c => c.Compensation)
            .IsInEnum().WithMessage(ReturnMessages.LinesRequired)
            .OverridePropertyName(ReturnFields.Compensation);
    }
}
