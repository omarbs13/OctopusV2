using FluentValidation;
using Pos.Domain.Business;

namespace Pos.Application.Business.SaveBusinessProfile;

public sealed class SaveBusinessProfileValidator : AbstractValidator<SaveBusinessProfileCommand>
{
    public SaveBusinessProfileValidator()
    {
        RuleFor(c => BusinessProfile.NormalizeText(c.TradeName))
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(BusinessMessages.TradeNameRequired)
            .MaximumLength(BusinessProfile.TradeNameMaxLength).WithMessage(BusinessMessages.TradeNameTooLong)
            .OverridePropertyName(BusinessFields.TradeName);

        RuleFor(c => BusinessProfile.NormalizeText(c.Address))
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(BusinessMessages.AddressRequired)
            .MaximumLength(BusinessProfile.AddressMaxLength).WithMessage(BusinessMessages.AddressTooLong)
            .OverridePropertyName(BusinessFields.Address);

        RuleFor(c => BusinessProfile.NormalizeText(c.Phone))
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(BusinessMessages.PhoneRequired)
            .MaximumLength(BusinessProfile.PhoneMaxLength).WithMessage(BusinessMessages.PhoneTooLong)
            .OverridePropertyName(BusinessFields.Phone);

        RuleFor(c => BusinessProfile.NormalizeTaxId(c.TaxId))
            .Must(BusinessProfile.IsValidTaxId).WithMessage(BusinessMessages.TaxIdTooLong)
            .OverridePropertyName(BusinessFields.TaxId);

        RuleFor(c => BusinessProfile.NormalizeFooter(c.FooterMessage))
            .Must(BusinessProfile.IsValidFooter).WithMessage(BusinessMessages.FooterTooLong)
            .OverridePropertyName(BusinessFields.FooterMessage);
    }
}
