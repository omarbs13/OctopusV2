using FluentValidation;

namespace Pos.Application.Suppliers.CreateSupplier;

public sealed class CreateSupplierValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierValidator() =>
        SupplierRules.Apply(this, c => c.Name, c => c.TaxId, c => c.Phone, c => c.Email, c => c.Address, c => c.PaymentTerms, c => c.CreditDaysText);
}
