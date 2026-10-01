using FluentValidation;
using Pos.Domain.Receivables;

namespace Pos.Application.Receivables.RegisterCustomerPayment;

/// <summary>Forma de la entrada; el monto contra el saldo lo verifica el manejador dentro de la transacción.</summary>
public sealed class RegisterCustomerPaymentValidator : AbstractValidator<RegisterCustomerPaymentCommand>
{
    public RegisterCustomerPaymentValidator()
    {
        RuleFor(c => c.RequestId)
            .NotEqual(Guid.Empty).WithMessage(ReceivableMessages.RequestRequired)
            .OverridePropertyName(ReceivableFields.RequestId);

        RuleFor(c => c.Method)
            .Must(CustomerPayment.IsAllowedMethod).WithMessage(ReceivableMessages.MethodInvalid)
            .OverridePropertyName(ReceivableFields.Method);

        RuleFor(c => c.Reference)
            .Must(r => string.IsNullOrWhiteSpace(r) || r.Trim().Length <= CustomerPayment.ReferenceMaxLength)
            .WithMessage(ReceivableMessages.ReferenceTooLong)
            .OverridePropertyName(ReceivableFields.Reference);
    }
}
