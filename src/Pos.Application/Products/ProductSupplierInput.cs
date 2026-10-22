using Pos.Application.Abstractions;
using Pos.Domain.Common;
using Pos.Domain.Products;

namespace Pos.Application.Products;

/// <summary>
/// Resultado de interpretar los campos del proveedor de una captura: valores ya convertidos y los errores
/// por campo (026). Los campos de loseta solo cuentan si la unidad es caja o metro cuadrado.
/// </summary>
internal sealed record ProductSupplierInput(
    long? PackagingThousandths,
    int? PiecesPerBox,
    long? PricePerSqmCents,
    long? PricePerBoxCents,
    long? PiecePriceCents,
    IReadOnlyList<FieldError> Errors)
{
    /// <summary>Texto que significa "el embalaje no aplica".</summary>
    private const string NotApplicable = "NA";

    public static ProductSupplierInput Parse(IProductInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var errors = new List<FieldError>();
        var isTile = UnitOfMeasure.IsTile(input.UnitCode);

        var piece = ParsePrice(input.PiecePriceText, ProductFields.Price, errors, required: !isTile);
        long? sqm = null;
        long? box = null;
        long? packaging = null;
        int? pieces = null;
        if (isTile)
        {
            sqm = ParsePrice(input.PricePerSqmText, ProductFields.PricePerSqm, errors, required: false);
            box = ParsePrice(input.PricePerBoxText, ProductFields.PricePerBox, errors, required: false);
            if (sqm is null && box is null && !errors.Any(e => e.Field is ProductFields.PricePerSqm or ProductFields.PricePerBox))
            {
                errors.Add(new FieldError(ProductFields.PricePerBox, ProductMessages.TilePriceRequired));
            }

            packaging = ParsePackaging(input.PackagingText, errors);
            pieces = ParsePieces(input.PiecesPerBoxText, errors);
        }

        return new ProductSupplierInput(packaging, pieces, sqm, box, piece, errors);
    }

    /// <summary>Datos del dominio a partir de la captura ya validada.</summary>
    public ProductDetails ToDetails(IProductInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new ProductDetails(
            Product.NormalizeName(input.Model),
            Clean(input.Measures),
            Clean(input.Brand),
            input.SupplierId,
            PackagingThousandths,
            PiecesPerBox,
            PricePerSqmCents,
            PricePerBoxCents,
            PiecePriceCents);
    }

    /// <summary>Advertencia (no bloquea) si X CAJA se aleja más del 2 % de X M2 × Embalaje.</summary>
    public IReadOnlyList<string> Warnings() =>
        PricePerBoxCents is { } box && PricePerSqmCents is { } sqm && PackagingThousandths is { } packaging
        && ProductPricing.ExceedsTolerance(box, sqm, packaging)
            ? [ProductMessages.PriceDifference]
            : [];

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static long? ParsePrice(string? text, string field, List<FieldError> errors, bool required)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            if (required)
            {
                errors.Add(new FieldError(field, ProductMessages.PriceRequired));
            }

            return null;
        }

        var result = Money.Parse(text);
        var message = result.Error switch
        {
            MoneyParseError.Format => ProductMessages.PriceFormat,
            MoneyParseError.TooManyDecimals => ProductMessages.PriceTooManyDecimals,
            MoneyParseError.TooLarge => ProductMessages.PriceTooLarge,
            _ when result.Value is not { } price || !Product.IsValidPrice(price) => ProductMessages.PriceNotPositive,
            _ => null,
        };
        if (message is not null)
        {
            errors.Add(new FieldError(field, message));
            return null;
        }

        return result.Value!.Value.Cents;
    }

    private static long? ParsePackaging(string? text, List<FieldError> errors)
    {
        if (string.IsNullOrWhiteSpace(text) || string.Equals(text.Trim(), NotApplicable, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var result = Quantity.Parse(text, decimalPlaces: 3, allowZero: true);
        if (result.Value is not { } quantity)
        {
            errors.Add(new FieldError(ProductFields.Packaging, ProductMessages.PackagingInvalid));
            return null;
        }

        if (quantity.Thousandths <= 0)
        {
            errors.Add(new FieldError(ProductFields.Packaging, ProductMessages.PackagingNotPositive));
            return null;
        }

        return quantity.Thousandths;
    }

    private static int? ParsePieces(string? text, List<FieldError> errors)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (!int.TryParse(text.Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var pieces)
            || pieces <= 0)
        {
            errors.Add(new FieldError(ProductFields.PiecesPerBox, ProductMessages.PiecesPerBoxInvalid));
            return null;
        }

        return pieces;
    }
}
