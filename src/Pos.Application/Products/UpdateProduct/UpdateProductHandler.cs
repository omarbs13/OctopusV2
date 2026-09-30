using FluentValidation;
using Pos.Application.Abstractions;
using Pos.Domain.Common;
using Pos.Domain.Products;

namespace Pos.Application.Products.UpdateProduct;

public sealed class UpdateProductHandler
{
    private readonly IProductRepository _products;
    private readonly IValidator<UpdateProductCommand> _validator;

    public UpdateProductHandler(IProductRepository products, IValidator<UpdateProductCommand> validator)
    {
        _products = products;
        _validator = validator;
    }

    public async Task<Result<ProductDto>> HandleAsync(UpdateProductCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<ProductDto>(ProductRules.ToError(validation));
        }

        // La imagen solo se carga si va a cambiar (003, FR-027).
        var imageChanges = command.Image is not null and not ProductImageChange.Keep;
        var product = await _products.GetAsync(command.Id, includeImage: imageChanges, cancellationToken);
        if (product is null)
        {
            return Result.Failure<ProductDto>(new NotFound());
        }

        if (product.Version != command.ExpectedVersion)
        {
            return Result.Failure<ProductDto>(new Conflict());
        }

        var sku = Product.NormalizeSku(command.Sku);
        var barcode = Product.NormalizeBarcode(command.Barcode);

        if (await _products.SkuExistsAsync(sku, product.Id, cancellationToken))
        {
            return Result.Failure<ProductDto>(new Duplicate(ProductFields.Sku));
        }

        if (barcode is not null && await _products.BarcodeExistsAsync(barcode, product.Id, cancellationToken))
        {
            return Result.Failure<ProductDto>(new Duplicate(ProductFields.Barcode));
        }

        var price = Money.Parse(command.PriceText).Value!.Value;
        product.Update(command.Name, sku, barcode, price, command.UnitCode, command.IsActive);
        ProductImages.Apply(product, command.Image);

        var outcome = await _products.SaveChangesAsync(product, command.ExpectedVersion, cancellationToken);
        return outcome.Status switch
        {
            SaveStatus.Saved => Result.Success(product.ToDto()),
            SaveStatus.Duplicate => Result.Failure<ProductDto>(new Duplicate(outcome.DuplicateField!)),
            _ => Result.Failure<ProductDto>(new Conflict()),
        };
    }
}
