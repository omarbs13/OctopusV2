using FluentValidation;

namespace Pos.Application.Customers.UpdateCustomer;

public sealed class UpdateCustomerValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerValidator() =>
        CustomerRules.Apply(this, c => c.Name, c => c.Phone, c => c.Email, c => c.TaxId, c => c.CreditLimitCents, c => c.CreditMode);
}
