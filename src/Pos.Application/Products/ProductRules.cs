using FluentValidation;
using Pos.Domain.Common;
using Pos.Domain.Products;

using Pos.Application.Abstractions;
using Pos.Application.Inventory;

namespace Pos.Application.Products;

/// <summary>Reglas de entrada de producto compartidas por el alta y la edición.</summary>
internal static class ProductRules
{
    public static void Apply<T>(AbstractValidator<T> validator)
        where T : IProductInput
    {
        validator.RuleFor(x => Product.NormalizeName(x.Model))
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(ProductMessages.ModelRequired)
            .MaximumLength(Product.ModelCaptureMaxLength).WithMessage(ProductMessages.ModelTooLong)
            .OverridePropertyName(ProductFields.Model);

        // El SKU es opcional: vacío genera uno automático (026).
        validator.RuleFor(x => Product.NormalizeSku(x.Sku))
            .Cascade(CascadeMode.Stop)
            .MaximumLength(Product.SkuMaxLength).WithMessage(ProductMessages.SkuTooLong)
            .Must(s => !s.Any(char.IsWhiteSpace)).WithMessage(ProductMessages.SkuWithSpaces)
            .OverridePropertyName(ProductFields.Sku);

        validator.RuleFor(x => Product.NormalizeBarcode(x.Barcode))
            .Must(Product.IsValidBarcode).WithMessage(ProductMessages.BarcodeFormat)
            .OverridePropertyName(ProductFields.Barcode);

        validator.RuleFor(x => x.Measures)
            .Must(m => (m ?? string.Empty).Trim().Length <= Product.MeasuresMaxLength).WithMessage(ProductMessages.MeasuresTooLong)
            .OverridePropertyName(ProductFields.Measures);

        validator.RuleFor(x => x.Brand)
            .Must(b => (b ?? string.Empty).Trim().Length <= Product.BrandMaxLength).WithMessage(ProductMessages.BrandTooLong)
            .OverridePropertyName(ProductFields.Brand);

        // Precios, embalaje y piezas por caja: un error por campo, con la regla de loseta / no loseta.
        validator.RuleFor(x => x).Custom((x, context) =>
        {
            foreach (var error in ProductSupplierInput.Parse(x).Errors)
            {
                context.AddFailure(error.Field, error.Message);
            }
        });

        validator.RuleFor(x => x.UnitCode)
            .Must(UnitOfMeasure.IsValidCode).WithMessage(ProductMessages.UnitRequired)
            .OverridePropertyName(ProductFields.UnitCode);

        validator.RuleFor(x => ParseMinimum(x.TracksInventory, x.MinimumStockText, x.UnitCode))
            .Must(r => r is not { Error: not null })
            .WithMessage(x =>
            {
                var unit = UnitOfMeasure.Find(x.UnitCode);
                var error = ParseMinimum(x.TracksInventory, x.MinimumStockText, x.UnitCode)!.Value.Error!.Value;
                return InventoryMessages.ForMinimum(error, unit?.Name ?? string.Empty, unit?.DecimalPlaces ?? 0);
            })
            .OverridePropertyName(ProductFields.MinimumStock);

        validator.RuleFor(x => ParseReorderPoint(x.TracksInventory, x.ReorderPointText, x.UnitCode))
            .Must(r => r is not { Error: not null })
            .WithMessage(x =>
            {
                var unit = UnitOfMeasure.Find(x.UnitCode);
                var error = ParseReorderPoint(x.TracksInventory, x.ReorderPointText, x.UnitCode)!.Value.Error!.Value;
                return InventoryMessages.ForReorderPoint(error, unit?.Name ?? string.Empty, unit?.DecimalPlaces ?? 0);
            })
            .OverridePropertyName(ProductFields.ReorderPoint);

        // Solo con ambos umbrales capturados y válidos: el punto de reorden debe ser menor que el mínimo (FR-002).
        validator.RuleFor(x => IsBelowMinimum(
                ParseMinimum(x.TracksInventory, x.MinimumStockText, x.UnitCode),
                ParseReorderPoint(x.TracksInventory, x.ReorderPointText, x.UnitCode)))
            .Must(below => below)
            .WithMessage(ProductMessages.ReorderPointNotBelowMinimum)
            .OverridePropertyName(ProductFields.ReorderPoint);
    }

    /// <summary>
    /// Punto de reorden capturado, con los decimales de la unidad elegida; se ignora si el producto no
    /// controla inventario. Vacío significa sin punto de reorden; 0 es válido (022).
    /// </summary>
    public static QuantityParseResult? ParseReorderPoint(bool tracksInventory, string? text, string? unitCode) =>
        ParseMinimum(tracksInventory, text, unitCode);

    private static bool IsBelowMinimum(QuantityParseResult? minimum, QuantityParseResult? reorder) =>
        minimum?.Value is not { } min || reorder?.Value is not { } point || point < min;

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
