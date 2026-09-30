using FluentValidation;

namespace Pos.Application.Products.UpdateProduct;

public sealed class UpdateProductValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductValidator() =>
        ProductRules.Apply(this, c => c.Name, c => c.Sku, c => c.Barcode, c => c.PriceText, c => c.UnitCode, c => c.TracksInventory, c => c.MinimumStockText);
}
