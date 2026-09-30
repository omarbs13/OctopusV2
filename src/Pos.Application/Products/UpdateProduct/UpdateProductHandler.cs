using FluentValidation;
using Pos.Application.Abstractions;
using Pos.Application.Inventory;
using Pos.Domain.Common;
using Pos.Domain.Products;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Products.UpdateProduct;

public sealed class UpdateProductHandler
{
    private readonly IAccessControl _access;
    private readonly IProductRepository _products;
    private readonly IValidator<UpdateProductCommand> _validator;
    private readonly IInventoryRepository _inventory;
    private readonly IWriteTransactions _transactions;

    public UpdateProductHandler(
        IAccessControl access,
        IProductRepository products,
        IValidator<UpdateProductCommand> validator,
        IInventoryRepository inventory,
        IWriteTransactions transactions)
    {
        _access = access;
        _products = products;
        _validator = validator;
        _inventory = inventory;
        _transactions = transactions;
    }

    public async Task<Result<ProductDto>> HandleAsync(UpdateProductCommand command, CancellationToken cancellationToken)
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

        // La transacción de escritura evita que se cuele un movimiento entre la comprobación de
        // "tiene movimientos" y el guardado (research §5).
        await using var transaction = await _transactions.BeginAsync(cancellationToken);

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

        var stock = await _inventory.GetStockAsync(product.Id, cancellationToken);
        var hasMovements = stock is not null;
        var inventoryErrors = new List<FieldError>();
        if (!Product.CanChangeInventorySettings(hasMovements, product.UnitCode, command.UnitCode, product.TracksInventory, command.TracksInventory))
        {
            if (!string.Equals(product.UnitCode, command.UnitCode, StringComparison.Ordinal))
            {
                inventoryErrors.Add(new FieldError(ProductFields.UnitCode, InventoryMessages.UnitLocked));
            }

            if (product.TracksInventory && !command.TracksInventory)
            {
                inventoryErrors.Add(new FieldError(ProductFields.TracksInventory, InventoryMessages.TrackingLocked));
            }
        }

        if (inventoryErrors.Count > 0)
        {
            return Result.Failure<ProductDto>(new ValidationFailed(inventoryErrors));
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
        var minimum = ProductRules.ParseMinimum(command.TracksInventory, command.MinimumStockText, command.UnitCode)?.Value;
        product.Update(command.Name, sku, barcode, price, command.UnitCode, command.IsActive, command.TracksInventory, minimum, hasMovements);
        ProductImages.Apply(product, command.Image);

        var outcome = await _products.SaveChangesAsync(product, command.ExpectedVersion, cancellationToken);
        if (outcome.Status == SaveStatus.Saved)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return outcome.Status switch
        {
            SaveStatus.Saved => Result.Success(product.ToDto(stock)),
            SaveStatus.Duplicate => Result.Failure<ProductDto>(new Duplicate(outcome.DuplicateField!)),
            _ => Result.Failure<ProductDto>(new Conflict()),
        };
    }
}
