using FluentValidation;
using Pos.Application.Abstractions;
using Pos.Domain.Common;
using Pos.Domain.Products;

namespace Pos.Application.Products.CreateProduct;

public sealed class CreateProductHandler
{
    private readonly IProductRepository _products;
    private readonly IValidator<CreateProductCommand> _validator;

    public CreateProductHandler(IProductRepository products, IValidator<CreateProductCommand> validator)
    {
        _products = products;
        _validator = validator;
    }

    public async Task<Result<ProductDto>> HandleAsync(CreateProductCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<ProductDto>(ProductRules.ToError(validation));
        }

        var sku = Product.NormalizeSku(command.Sku);
        var barcode = Product.NormalizeBarcode(command.Barcode);

        if (await _products.SkuExistsAsync(sku, null, cancellationToken))
        {
            return Result.Failure<ProductDto>(new Duplicate(ProductFields.Sku));
        }

        if (barcode is not null && await _products.BarcodeExistsAsync(barcode, null, cancellationToken))
        {
            return Result.Failure<ProductDto>(new Duplicate(ProductFields.Barcode));
        }

        var price = Money.Parse(command.PriceText).Value!.Value;
        var product = Product.Create(command.Name, sku, barcode, price, command.UnitCode);
        ProductImages.Apply(product, command.Image);
        _products.Add(product);

        var outcome = await _products.SaveChangesAsync(product, expectedVersion: null, cancellationToken);
        return outcome.Status switch
        {
            SaveStatus.Saved => Result.Success(product.ToDto()),
            SaveStatus.Duplicate => Result.Failure<ProductDto>(new Duplicate(outcome.DuplicateField!)),
            _ => Result.Failure<ProductDto>(new Conflict()),
        };
    }
}
