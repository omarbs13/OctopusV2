using FluentValidation;

namespace Pos.Application.Categories.UpdateCategory;

public sealed class UpdateCategoryValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryValidator() => CategoryRules.Apply(this, c => c.Name, c => c.Description);
}
