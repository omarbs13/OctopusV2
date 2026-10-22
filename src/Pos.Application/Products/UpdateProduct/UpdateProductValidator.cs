using FluentValidation;

namespace Pos.Application.Products.UpdateProduct;

public sealed class UpdateProductValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductValidator() => ProductRules.Apply(this);
}
