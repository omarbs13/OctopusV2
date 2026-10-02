using FluentValidation;
using Pos.Domain.Common;
using Pos.Domain.Purchases;

namespace Pos.Application.Purchases.RegisterPurchase;

/// <summary>
/// Validación de forma de la compra (contracts/application-ports.md, paso 1). Los decimales de cada unidad, el
/// estado de proveedor y productos y los importes se revisan después con la base de datos.
/// </summary>
public sealed class RegisterPurchaseValidator : AbstractValidator<RegisterPurchaseCommand>
{
    public RegisterPurchaseValidator()
    {
        RuleFor(c => c.SupplierId)
            .NotEqual(Guid.Empty).WithMessage(PurchaseMessages.SupplierRequired)
            .OverridePropertyName(PurchaseFields.SupplierId);

        RuleFor(c => c.InvoiceNumber)
            .Cascade(CascadeMode.Stop)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage(PurchaseMessages.InvoiceRequired)
            .Must(n => n.Trim().Length <= Purchase.InvoiceNumberMaxLength).WithMessage(PurchaseMessages.InvoiceTooLong)
            .OverridePropertyName(PurchaseFields.InvoiceNumber);

        RuleFor(c => c.InvoiceDate)
            .NotNull().WithMessage(PurchaseMessages.InvoiceDateRequired)
            .OverridePropertyName(PurchaseFields.InvoiceDate);

        RuleFor(c => c.Lines)
            .Must(l => l is { Count: > 0 }).WithMessage(PurchaseMessages.LinesRequired)
            .OverridePropertyName(PurchaseFields.Lines);

        RuleFor(c => c.TaxText)
            .Must(t => string.IsNullOrWhiteSpace(t) || Money.Parse(t).Error is null).WithMessage(PurchaseMessages.TaxFormat)
            .OverridePropertyName(PurchaseFields.Tax);

        RuleFor(c => c).Custom((command, context) =>
        {
            if (command.Lines is null)
            {
                return;
            }

            var seen = new HashSet<Guid>();
            for (var i = 0; i < command.Lines.Count; i++)
            {
                var line = command.Lines[i];
                if (line.ProductId == Guid.Empty)
                {
                    context.AddFailure(PurchaseFields.LineProduct(i), PurchaseMessages.ProductRequired);
                }
                else if (!seen.Add(line.ProductId))
                {
                    context.AddFailure(PurchaseFields.LineProduct(i), PurchaseMessages.ProductRepeated);
                }

                // Con 3 decimales: el límite de cada unidad se revisa con su producto (paso 5).
                if (Quantity.Parse(line.QuantityText, 3).Error is { } quantityError)
                {
                    context.AddFailure(PurchaseFields.LineQuantity(i), Inventory.InventoryMessages.ForQuantity(quantityError, string.Empty, 3));
                }

                if (Money.Parse(line.UnitCostText).Error is { } costError)
                {
                    context.AddFailure(PurchaseFields.LineUnitCost(i), PurchaseMessages.ForCost(costError));
                }
            }
        });
    }
}
