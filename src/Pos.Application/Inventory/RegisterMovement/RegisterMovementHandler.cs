using FluentValidation;
using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Products;
using Pos.Domain.Common;
using Pos.Domain.Inventory;
using Pos.Domain.Products;
using Pos.Application.Users.Access;
using Pos.Application.Users.Session;
using Pos.Domain.Users;

namespace Pos.Application.Inventory.RegisterMovement;

/// <summary>
/// Registra un movimiento y actualiza la existencia en una sola transacción de escritura
/// (FR-007 a FR-015). Las violaciones de reglas se devuelven como <see cref="ValidationFailed"/>.
/// </summary>
public sealed partial class RegisterMovementHandler
{
    private readonly IAccessControl _access;
    private readonly IUserSession _session;
    private readonly IProductRepository _products;
    private readonly IInventoryRepository _inventory;
    private readonly IWriteTransactions _transactions;
    private readonly IValidator<RegisterMovementCommand> _validator;
    private readonly ILogger<RegisterMovementHandler> _logger;

    public RegisterMovementHandler(
        IAccessControl access,
        IUserSession session,
        IProductRepository products,
        IInventoryRepository inventory,
        IWriteTransactions transactions,
        IValidator<RegisterMovementCommand> validator,
        ILogger<RegisterMovementHandler> logger)
    {
        _access = access;
        _session = session;
        _products = products;
        _inventory = inventory;
        _transactions = transactions;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<MovementDto>> HandleAsync(RegisterMovementCommand command, CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(Permission.RegisterMovements, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<MovementDto>(access.Error!);
        }

        ArgumentNullException.ThrowIfNull(command);

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<MovementDto>(ProductRules.ToError(validation));
        }

        await using var transaction = await _transactions.BeginAsync(cancellationToken);

        var product = await _products.GetAsync(command.ProductId, includeImage: false, cancellationToken);
        if (product is null)
        {
            return Result.Failure<MovementDto>(new NotFound());
        }

        var unit = UnitOfMeasure.Find(product.UnitCode)!;
        var stock = await _inventory.GetStockAsync(product.Id, cancellationToken);
        var isNewStock = stock is null;
        stock ??= ProductStock.Start(product.Id);

        var errors = Validate(command, product, unit, stock, out var quantity);
        if (errors.Count > 0)
        {
            return Result.Failure<MovementDto>(new ValidationFailed(errors));
        }

        var movement = stock.Record(
            command.Type, quantity, unit, product.IsActive, product.TracksInventory, command.Reason, command.Reference);
        if (isNewStock)
        {
            _inventory.AddStock(stock);
        }

        _inventory.AddMovement(movement);

        var outcome = await _inventory.SaveChangesAsync(cancellationToken);
        if (outcome.Status != SaveStatus.Saved)
        {
            LogConflict(product.Id, command.Type, quantity.Thousandths);
            return Result.Failure<MovementDto>(new Conflict());
        }

        await transaction.CommitAsync(cancellationToken);

        return Result.Success(new MovementDto(
            movement.Id,
            movement.CreatedAt,
            product.Id,
            product.Name,
            product.Sku,
            unit.Name,
            unit.DecimalPlaces,
            movement.Type,
            movement.Quantity.Thousandths,
            movement.ResultingStock.Thousandths,
            movement.Reason,
            movement.Reference,
            movement.CreatedBy,
            _session.User?.UserName ?? SystemUser.NameOf(movement.CreatedBy)));
    }

    private static List<FieldError> Validate(
        RegisterMovementCommand command,
        Product product,
        UnitOfMeasure unit,
        ProductStock stock,
        out Quantity quantity)
    {
        var errors = new List<FieldError>();

        if (!product.TracksInventory)
        {
            errors.Add(new FieldError(InventoryFields.ProductId, InventoryMessages.NotTracked));
        }
        else if (!product.IsActive)
        {
            errors.Add(new FieldError(InventoryFields.ProductId, InventoryMessages.ProductInactive));
        }

        if (command.Type == MovementType.Initial && !ProductStock.CanRecordInitial(stock.MovementCount))
        {
            errors.Add(new FieldError(InventoryFields.Type, InventoryMessages.InitialNotAllowed));
        }

        if (command.Type.RequiresReason() && InventoryMovement.NormalizeText(command.Reason) is null)
        {
            errors.Add(new FieldError(InventoryFields.Reason, InventoryMessages.ReasonRequired));
        }

        var parsed = Quantity.Parse(command.QuantityText, unit.DecimalPlaces);
        quantity = parsed.Value ?? Quantity.Zero;
        if (parsed.Value is not { } value)
        {
            errors.Add(new FieldError(
                InventoryFields.Quantity,
                InventoryMessages.ForQuantity(parsed.Error!.Value, unit.Name, unit.DecimalPlaces)));
        }
        else if (!command.Type.IsIncrease() && ProductStock.WouldGoNegative(stock.OnHand, value))
        {
            errors.Add(new FieldError(
                InventoryFields.Quantity,
                InventoryMessages.WouldGoNegative(
                    InventoryMessages.Format(stock.OnHand.Thousandths, unit.DecimalPlaces), unit.Name)));
        }
        else if (command.Type.IsIncrease() && ProductStock.WouldExceedMaximum(stock.OnHand, value))
        {
            errors.Add(new FieldError(InventoryFields.Quantity, InventoryMessages.StockExceeded));
        }

        return errors;
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Movimiento de inventario rechazado por conflicto. ProductId={ProductId} Type={Type} QuantityThousandths={Quantity}")]
    private partial void LogConflict(Guid productId, MovementType type, long quantity);
}
