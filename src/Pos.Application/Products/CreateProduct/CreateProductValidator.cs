using FluentValidation;

namespace Pos.Application.Products.CreateProduct;

public sealed class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator() =>
        ProductRules.Apply(this, c => c.Name, c => c.Sku, c => c.Barcode, c => c.PriceText, c => c.UnitCode, c => c.TracksInventory, c => c.MinimumStockText);
}
