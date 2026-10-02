using FluentValidation;
using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Categories;
using Pos.Domain.Categories;
using Pos.Domain.Common;
using Pos.Domain.Products;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Products.CreateProduct;

/// <summary>
/// Alta de producto. Con categoría, la validación (existe, no borrada y activa) y el guardado van en una
/// transacción de escritura, para que una desactivación o eliminación simultánea no se cuele (016, research §4).
/// </summary>
public sealed partial class CreateProductHandler
{
    private readonly IAccessControl _access;
    private readonly IProductRepository _products;
    private readonly IValidator<CreateProductCommand> _validator;
    private readonly ICategoryRepository _categories;
    private readonly IWriteTransactions _transactions;
    private readonly ILogger<CreateProductHandler> _logger;

    public CreateProductHandler(
        IAccessControl access,
        IProductRepository products,
        IValidator<CreateProductCommand> validator,
        ICategoryRepository categories,
        IWriteTransactions transactions,
        ILogger<CreateProductHandler> logger)
    {
        _access = access;
        _products = products;
        _validator = validator;
        _categories = categories;
        _transactions = transactions;
        _logger = logger;
    }

    public async Task<Result<ProductDto>> HandleAsync(CreateProductCommand command, CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(Permission.ManageProducts, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<ProductDto>(access.Error!);
        }

        ArgumentNullException.ThrowIfNull(command);

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<ProductDto>(ProductRules.ToError(validation));
        }

        await using var transaction = command.CategoryId is null ? null : await _transactions.BeginAsync(cancellationToken);

        Category? category = null;
        if (command.CategoryId is { } categoryId)
        {
            category = await _categories.GetAsync(categoryId, cancellationToken);
            if (category is not { IsActive: true })
            {
                LogNotAssignable(categoryId);
                return Result.Failure<ProductDto>(new CategoryNotAssignable());
            }
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
        var minimum = ProductRules.ParseMinimum(command.TracksInventory, command.MinimumStockText, command.UnitCode)?.Value;
        var product = Product.Create(command.Name, sku, barcode, price, command.UnitCode, command.TracksInventory, minimum, command.CategoryId);
        ProductImages.Apply(product, command.Image);
        _products.Add(product);

        var outcome = await _products.SaveChangesAsync(product, expectedVersion: null, cancellationToken);
        if (outcome.Status == SaveStatus.Saved && transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return outcome.Status switch
        {
            SaveStatus.Saved => Result.Success(product.ToDto(category: category)),
            SaveStatus.Duplicate => Result.Failure<ProductDto>(new Duplicate(outcome.DuplicateField!)),
            _ => Result.Failure<ProductDto>(new Conflict()),
        };
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Alta de producto rechazada: la categoría no está disponible. CategoryId={CategoryId}")]
    private partial void LogNotAssignable(Guid categoryId);
}
