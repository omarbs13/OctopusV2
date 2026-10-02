using FluentValidation;

namespace Pos.Application.Categories.CreateCategory;

public sealed class CreateCategoryValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryValidator() => CategoryRules.Apply(this, c => c.Name, c => c.Description);
}
