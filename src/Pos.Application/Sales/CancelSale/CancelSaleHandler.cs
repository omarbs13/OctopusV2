using FluentValidation;
using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.CashShifts;
using Pos.Application.Users.Access;
using Pos.Application.Inventory;
using Pos.Application.Products;
using Pos.Domain.CashShifts;
using Pos.Domain.Sales;
using Pos.Domain.Users;

namespace Pos.Application.Sales.CancelSale;

/// <summary>
/// Cancela una venta completa (research §9): regresa exactamente lo que salió por cada línea y deja
/// una entrada en la bitácora, todo en una transacción. Las ventas nunca se borran.
/// </summary>
public sealed partial class CancelSaleHandler
{
    public const string AuditAction = AuditActions.SaleCancelled;

    private readonly IAccessControl _access;
    private readonly ISaleRepository _sales;
    private readonly ICashShiftRepository _shifts;
    private readonly IInventoryRepository _inventory;
    private readonly IAuditLog _audit;
    private readonly IWriteTransactions _transactions;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<CancelSaleCommand> _validator;
    private readonly ILogger<CancelSaleHandler> _logger;

    public CancelSaleHandler(
        IAccessControl access,
        ISaleRepository sales,
        ICashShiftRepository shifts,
        IInventoryRepository inventory,
        IAuditLog audit,
        IWriteTransactions transactions,
        IClock clock,
        ICurrentUser currentUser,
        IValidator<CancelSaleCommand> validator,
        ILogger<CancelSaleHandler> logger)
    {
        _access = access;
        _sales = sales;
        _shifts = shifts;
        _inventory = inventory;
        _audit = audit;
        _transactions = transactions;
        _clock = clock;
        _currentUser = currentUser;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(CancelSaleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var access = await _access.CheckAsync(Permission.CancelSales, command.AuthorizationGrantId, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure(access.Error!);
        }

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure(ProductRules.ToError(validation));
        }

        await using var transaction = await _transactions.BeginAsync(cancellationToken);

        var sale = await _sales.GetAsync(command.SaleId, cancellationToken);
        if (sale is null)
        {
            return Result.Failure(new NotFound());
        }

        if (sale.Status != SaleStatus.Completed)
        {
            return Result.Failure(new InvalidState(SaleMessages.AlreadyCancelled));
        }

        if (sale.Version != command.ExpectedVersion)
        {
            return Result.Failure(new Conflict());
        }

        // 008: solo se cancelan ventas del turno abierto actual (incluye rechazar las anteriores a 0.6.0, sin turno).
        var shift = await _shifts.GetOpenAsync(CashRegister.Default, cancellationToken);
        if (shift is null || sale.CashShiftId != shift.Id)
        {
            return Result.Failure(new InvalidState(CashShiftMessages.SaleFromClosedShift));
        }

        var saleCash = await _sales.GetCashAppliedAsync(sale.Id, cancellationToken);
        if (saleCash > 0)
        {
            var expected = shift.ExpectedCash(await _sales.GetShiftTotalsAsync(shift.Id, cancellationToken));
            if (!CashShiftMath.CanRefund(expected, saleCash))
            {
                // Nunca se revela el monto esperado, a ningún rol (clarificación 1).
                LogInsufficientCash(sale.Id, shift.Id);
                return Result.Failure(new InsufficientCash(null));
            }
        }

        var reason = command.Reason.Trim();
        sale.Cancel(reason, _clock.UtcNow, _currentUser.UserId);

        var withMovement = sale.Lines.Where(l => l.SaleMovementId is not null).ToList();
        var stocks = withMovement.Count == 0
            ? new Dictionary<Guid, Pos.Domain.Inventory.ProductStock>()
            : (await _inventory.GetStocksAsync([.. withMovement.Select(l => l.ProductId).Distinct()], cancellationToken))
                .ToDictionary(s => s.Key, s => s.Value);

        foreach (var line in withMovement)
        {
            var stock = stocks[line.ProductId];
            var movement = stock.RecordSaleCancellation(line.Quantity, sale.Folio);
            _inventory.AddMovement(movement);
            sale.LinkCancellationMovement(line.Id, movement.Id);
        }

        _audit.Add(AuditAction, "Sale", sale.Id, $"Folio {sale.Folio}. Motivo: {reason}", access.AuthorizedBy);

        var outcome = await _sales.SaveChangesAsync(cancellationToken);
        if (outcome.Status != SaveStatus.Saved)
        {
            LogConflict(sale.Id);
            return Result.Failure(new Conflict());
        }

        await transaction.CommitAsync(cancellationToken);
        LogCancelled(sale.Id, sale.Folio);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Venta cancelada. SaleId={SaleId} Folio={Folio}")]
    private partial void LogCancelled(Guid saleId, string folio);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cancelación de venta rechazada por efectivo insuficiente. SaleId={SaleId} ShiftId={ShiftId}")]
    private partial void LogInsufficientCash(Guid saleId, Guid shiftId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cancelación de venta rechazada por conflicto. SaleId={SaleId}")]
    private partial void LogConflict(Guid saleId);
}
