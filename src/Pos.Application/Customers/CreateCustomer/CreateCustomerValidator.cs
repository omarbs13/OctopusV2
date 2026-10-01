using FluentValidation;

namespace Pos.Application.Customers.CreateCustomer;

public sealed class CreateCustomerValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerValidator() =>
        CustomerRules.Apply(this, c => c.Name, c => c.Phone, c => c.Email, c => c.TaxId, c => c.CreditLimitCents, c => c.CreditMode);
}
