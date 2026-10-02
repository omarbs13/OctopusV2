using FluentValidation;

namespace Pos.Application.Suppliers.UpdateSupplier;

public sealed class UpdateSupplierValidator : AbstractValidator<UpdateSupplierCommand>
{
    public UpdateSupplierValidator() =>
        SupplierRules.Apply(this, c => c.Name, c => c.TaxId, c => c.Phone, c => c.Email, c => c.Address, c => c.PaymentTerms, c => c.CreditDaysText);
}
