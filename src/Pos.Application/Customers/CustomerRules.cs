using System.Linq.Expressions;
using FluentValidation;
using Pos.Domain.Common;
using Pos.Domain.Customers;

namespace Pos.Application.Customers;

/// <summary>Reglas de validación comunes al alta y la edición de clientes (research §10).</summary>
internal static class CustomerRules
{
    public static void Apply<T>(
        AbstractValidator<T> validator,
        Expression<Func<T, string>> name,
        Expression<Func<T, string>> phone,
        Expression<Func<T, string?>> email,
        Expression<Func<T, string?>> taxId,
        Expression<Func<T, long?>> creditLimit,
        Expression<Func<T, CreditMode?>> creditMode)
    {
        validator.RuleFor(name)
            .Cascade(CascadeMode.Stop)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage(CustomerMessages.NameRequired)
            .Must(n => n.Trim().Length <= Customer.NameMaxLength).WithMessage(CustomerMessages.NameTooLong)
            .OverridePropertyName(CustomerFields.Name);

        validator.RuleFor(phone)
            .Cascade(CascadeMode.Stop)
            .Must(p => !string.IsNullOrWhiteSpace(p)).WithMessage(CustomerMessages.PhoneRequired)
            .Must(p => p.Trim().Length <= Customer.PhoneMaxLength).WithMessage(CustomerMessages.PhoneTooLong)
            .OverridePropertyName(CustomerFields.Phone);

        // Una sola regla de email: la del dominio, para que lo aceptado aquí nunca lo rechace el agregado.
        validator.RuleFor(email)
            .Cascade(CascadeMode.Stop)
            .Must(e => string.IsNullOrWhiteSpace(e) || e.Trim().Length <= Customer.EmailMaxLength).WithMessage(CustomerMessages.EmailTooLong)
            .Must(e => string.IsNullOrWhiteSpace(e) || Customer.IsValidEmail(e)).WithMessage(CustomerMessages.EmailInvalid)
            .OverridePropertyName(CustomerFields.Email);

        validator.RuleFor(taxId)
            .Must(t => Customer.NormalizeTaxId(t) is not { Length: > Customer.TaxIdMaxLength }).WithMessage(CustomerMessages.TaxIdTooLong)
            .OverridePropertyName(CustomerFields.TaxId);

        validator.RuleFor(creditLimit)
            .Must(l => l is null or (>= 0 and <= Money.MaxCents)).WithMessage(CustomerMessages.LimitInvalid)
            .OverridePropertyName(CustomerFields.CreditLimit);

        validator.RuleFor(creditMode)
            .Must(m => m is null || Enum.IsDefined(m.Value)).WithMessage(CustomerMessages.CreditModeInvalid)
            .OverridePropertyName(CustomerFields.CreditMode);
    }

    /// <summary>Texto de la bitácora con los datos del cliente; sin datos sensibles.</summary>
    public static string Describe(Customer customer) =>
        $"Cliente: {customer.Name}. RFC: {customer.TaxId ?? "—"}. Modalidad: {customer.CreditMode.ToCode()}. Límite: {Printing.Ticket.TicketBuilder.FormatMoney(customer.CreditLimitCents)}";
}
