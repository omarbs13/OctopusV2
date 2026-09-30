using FluentValidation;
using Pos.Domain.Common;
using Pos.Domain.Products;

using Pos.Application.Abstractions;

namespace Pos.Application.Products;

/// <summary>Reglas de entrada de producto compartidas por el alta y la edición.</summary>
internal static class ProductRules
{
    public static void Apply<T>(
        AbstractValidator<T> validator,
        Func<T, string?> name,
        Func<T, string?> sku,
        Func<T, string?> barcode,
        Func<T, string?> priceText)
    {
        validator.RuleFor(x => Product.NormalizeName(name(x)))
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(ProductMessages.NameRequired)
            .MaximumLength(Product.NameMaxLength).WithMessage(ProductMessages.NameTooLong)
            .OverridePropertyName(ProductFields.Name);

        validator.RuleFor(x => Product.NormalizeSku(sku(x)))
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(ProductMessages.SkuRequired)
            .MaximumLength(Product.SkuMaxLength).WithMessage(ProductMessages.SkuTooLong)
            .Must(s => !s.Any(char.IsWhiteSpace)).WithMessage(ProductMessages.SkuWithSpaces)
            .OverridePropertyName(ProductFields.Sku);

        validator.RuleFor(x => Product.NormalizeBarcode(barcode(x)))
            .Must(Product.IsValidBarcode).WithMessage(ProductMessages.BarcodeFormat)
            .OverridePropertyName(ProductFields.Barcode);

        validator.RuleFor(x => priceText(x))
            .Cascade(CascadeMode.Stop)
            .Must(p => !string.IsNullOrWhiteSpace(p)).WithMessage(ProductMessages.PriceRequired)
            .Must(p => Money.TryParse(p, out _)).WithMessage(ProductMessages.PriceFormat)
            .OverridePropertyName(ProductFields.Price);
    }

    public static ValidationFailed ToError(FluentValidation.Results.ValidationResult result) =>
        new([.. result.Errors.Select(e => new FieldError(e.PropertyName, e.ErrorMessage))]);
}
