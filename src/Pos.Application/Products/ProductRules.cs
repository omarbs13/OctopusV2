using FluentValidation;
using Pos.Domain.Common;
using Pos.Domain.Products;

using Pos.Application.Abstractions;
using Pos.Application.Inventory;

namespace Pos.Application.Products;

/// <summary>Reglas de entrada de producto compartidas por el alta y la edición.</summary>
internal static class ProductRules
{
    public static void Apply<T>(
        AbstractValidator<T> validator,
        Func<T, string?> name,
        Func<T, string?> sku,
        Func<T, string?> barcode,
        Func<T, string?> priceText,
        Func<T, string?> unitCode,
        Func<T, bool> tracksInventory,
        Func<T, string?> minimumStockText)
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

        validator.RuleFor(x => Money.Parse(priceText(x)))
            .Cascade(CascadeMode.Stop)
            .Must(r => r.Error != MoneyParseError.Empty).WithMessage(ProductMessages.PriceRequired)
            .Must(r => r.Error != MoneyParseError.Format).WithMessage(ProductMessages.PriceFormat)
            .Must(r => r.Error != MoneyParseError.TooManyDecimals).WithMessage(ProductMessages.PriceTooManyDecimals)
            .Must(r => r.Error != MoneyParseError.TooLarge).WithMessage(ProductMessages.PriceTooLarge)
            .Must(r => r.Value is { } price && Product.IsValidPrice(price)).WithMessage(ProductMessages.PriceNotPositive)
            .OverridePropertyName(ProductFields.Price);

        validator.RuleFor(x => unitCode(x))
            .Must(UnitOfMeasure.IsValidCode).WithMessage(ProductMessages.UnitRequired)
            .OverridePropertyName(ProductFields.UnitCode);

        validator.RuleFor(x => ParseMinimum(tracksInventory(x), minimumStockText(x), unitCode(x)))
            .Must(r => r is not { Error: not null })
            .WithMessage(x =>
            {
                var unit = UnitOfMeasure.Find(unitCode(x));
                var error = ParseMinimum(tracksInventory(x), minimumStockText(x), unitCode(x))!.Value.Error!.Value;
                return InventoryMessages.ForMinimum(error, unit?.Name ?? string.Empty, unit?.DecimalPlaces ?? 0);
            })
            .OverridePropertyName(ProductFields.MinimumStock);
    }

    /// <summary>
    /// Existencia mínima capturada, con los decimales de la unidad elegida; se ignora si el producto
    /// no controla inventario. Vacío significa sin mínimo.
    /// </summary>
    public static QuantityParseResult? ParseMinimum(bool tracksInventory, string? text, string? unitCode)
    {
        if (!tracksInventory || string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return Quantity.Parse(text, UnitOfMeasure.Find(unitCode)?.DecimalPlaces ?? 0, allowZero: true);
    }

    public static ValidationFailed ToError(FluentValidation.Results.ValidationResult result) =>
        new([.. result.Errors.Select(e => new FieldError(e.PropertyName, e.ErrorMessage))]);
}
