using FluentValidation;

namespace Pos.Application.Products.CreateProduct;

public sealed class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator() => ProductRules.Apply(this);
}
