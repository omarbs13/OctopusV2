using System.Linq.Expressions;
using FluentValidation;
using Pos.Domain.Categories;

namespace Pos.Application.Categories;

/// <summary>Reglas de validación comunes al alta y la edición de categorías (research §3).</summary>
internal static class CategoryRules
{
    public static void Apply<T>(
        AbstractValidator<T> validator,
        Expression<Func<T, string>> name,
        Expression<Func<T, string?>> description)
    {
        validator.RuleFor(name)
            .Cascade(CascadeMode.Stop)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage(CategoryMessages.NameRequired)
            .Must(n => Category.IsValidName(Category.NormalizeName(n))).WithMessage(CategoryMessages.NameTooLong)
            .OverridePropertyName(CategoryFields.Name);

        validator.RuleFor(description)
            .Must(d => Category.IsValidDescription(Category.NormalizeDescription(d))).WithMessage(CategoryMessages.DescriptionTooLong)
            .OverridePropertyName(CategoryFields.Description);
    }

    /// <summary>Texto de la bitácora con los datos de la categoría.</summary>
    public static string Describe(Category category) => $"Categoría: {category.Name}";
}
