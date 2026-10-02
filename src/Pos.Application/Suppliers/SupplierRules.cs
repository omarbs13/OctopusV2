using System.Globalization;
using System.Linq.Expressions;
using FluentValidation;
using Pos.Domain.Common;
using Pos.Domain.Suppliers;

namespace Pos.Application.Suppliers;

/// <summary>Reglas de validación comunes al alta y la edición de proveedores (research §9).</summary>
internal static class SupplierRules
{
    public static void Apply<T>(
        AbstractValidator<T> validator,
        Expression<Func<T, string>> name,
        Expression<Func<T, string?>> taxId,
        Expression<Func<T, string?>> phone,
        Expression<Func<T, string?>> email,
        Expression<Func<T, string?>> address,
        Expression<Func<T, PaymentTerms>> paymentTerms,
        Func<T, string?> creditDaysText)
    {
        validator.RuleFor(name)
            .Cascade(CascadeMode.Stop)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage(SupplierMessages.NameRequired)
            .Must(n => n.Trim().Length <= Supplier.NameMaxLength).WithMessage(SupplierMessages.NameTooLong)
            .OverridePropertyName(SupplierFields.Name);

        validator.RuleFor(taxId)
            .Must(t => Supplier.NormalizeTaxId(t) is not { Length: > Supplier.TaxIdMaxLength }).WithMessage(SupplierMessages.TaxIdTooLong)
            .OverridePropertyName(SupplierFields.TaxId);

        validator.RuleFor(phone)
            .Must(p => string.IsNullOrWhiteSpace(p) || p.Trim().Length <= Supplier.PhoneMaxLength).WithMessage(SupplierMessages.PhoneTooLong)
            .OverridePropertyName(SupplierFields.Phone);

        // Una sola regla de email: la del dominio, para que lo aceptado aquí nunca lo rechace el agregado.
        validator.RuleFor(email)
            .Cascade(CascadeMode.Stop)
            .Must(e => string.IsNullOrWhiteSpace(e) || e.Trim().Length <= Supplier.EmailMaxLength).WithMessage(SupplierMessages.EmailTooLong)
            .Must(e => string.IsNullOrWhiteSpace(e) || EmailAddress.IsValid(e)).WithMessage(SupplierMessages.EmailInvalid)
            .OverridePropertyName(SupplierFields.Email);

        validator.RuleFor(address)
            .Must(a => string.IsNullOrWhiteSpace(a) || a.Trim().Length <= Supplier.AddressMaxLength).WithMessage(SupplierMessages.AddressTooLong)
            .OverridePropertyName(SupplierFields.Address);

        validator.RuleFor(paymentTerms)
            .Must(Enum.IsDefined).WithMessage(SupplierMessages.PaymentTermsInvalid)
            .OverridePropertyName(SupplierFields.PaymentTerms);

        var termsOf = paymentTerms.Compile();
        validator.RuleFor(c => c)
            .Must(c => ParseCreditDays(creditDaysText(c)) is not null)
            .When(c => termsOf(c) == PaymentTerms.Credit)
            .WithMessage(SupplierMessages.CreditDaysInvalid)
            .OverridePropertyName(SupplierFields.CreditDays);
    }

    /// <summary>Días de crédito capturados: entero de 1 a 365, o nulo si está vacío o no es válido.</summary>
    public static int? ParseCreditDays(string? text) =>
        int.TryParse(text?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var days)
        && days is >= Supplier.MinCreditDays and <= Supplier.MaxCreditDays
            ? days
            : null;
}
