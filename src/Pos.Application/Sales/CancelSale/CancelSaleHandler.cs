using FluentValidation;
using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Discounts;
using Pos.Application.Audit;
using Pos.Application.CashShifts;
using Pos.Application.Users.Access;
using Pos.Application.Inventory;
using Pos.Application.Licensing;
using Pos.Application.Products;
using Pos.Application.Receivables;
using Pos.Application.Returns;
using Pos.Domain.CashShifts;
using Pos.Domain.Licensing;
using Pos.Domain.Returns;
using Pos.Domain.Sales;
using Pos.Domain.Users;

namespace Pos.Application.Sales.CancelSale;

/// <summary>
/// Cancela una venta completa (research §9): regresa exactamente lo que salió por cada línea y deja
/// una entrada en la bitácora, todo en una transacción. Las ventas nunca se borran.
/// Con el módulo Devoluciones activo (013) delega en <see cref="SaleReturnProcessor"/>: exige la
/// autorización de un Administrador y compensa al cliente con reintegro o nota de crédito. Sin el
/// módulo conserva la cancelación básica de 005/008 (research §12); en ella una venta a crédito también
/// ajusta su cuenta por cobrar en la misma transacción (014, research §8).
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
    private readonly ILicenseState? _license;
    private readonly SaleReturnProcessor? _processor;
    private readonly CreditSettlementService? _creditSettlement;
    private readonly CouponUseRelease? _couponRelease;

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
        ILogger<CancelSaleHandler> logger,
        ILicenseState? license = null,
        SaleReturnProcessor? processor = null,
        CreditSettlementService? creditSettlement = null,
        CouponUseRelease? couponRelease = null)
    {
        _creditSettlement = creditSettlement;
        _couponRelease = couponRelease;
        _license = license;
        _processor = processor;
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

    public async Task<Result<ReturnResult>> HandleAsync(CancelSaleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (_processor is not null && _license?.IsModuleActive(LicensedModule.Returns) != false)
        {
            var returnValidation = await _validator.ValidateAsync(command, cancellationToken);
            if (!returnValidation.IsValid)
            {
                return Result.Failure<ReturnResult>(ProductRules.ToError(returnValidation));
            }

            return await _processor.ProcessAsync(
                new ReturnRequest(
                    command.SaleId,
                    command.ExpectedVersion,
                    ReturnKind.Cancellation,
                    Lines: null,
                    command.Reason,
                    command.Compensation,
                    command.AuthorizationGrantId),
                cancellationToken);
        }

        return await CancelBasicAsync(command, cancellationToken);
    }

    /// <summary>Cancelación básica de 005/008, sin compensación ni registro de devolución.</summary>
    private async Task<Result<ReturnResult>> CancelBasicAsync(CancelSaleCommand command, CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(Permission.CancelSales, command.AuthorizationGrantId, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<ReturnResult>(access.Error!);
        }

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<ReturnResult>(ProductRules.ToError(validation));
        }

        await using var transaction = await _transactions.BeginAsync(cancellationToken);

        var sale = await _sales.GetAsync(command.SaleId, cancellationToken);
        if (sale is null)
        {
            return Result.Failure<ReturnResult>(new NotFound());
        }

        if (sale.Status != SaleStatus.Completed)
        {
            return Result.Failure<ReturnResult>(new InvalidState(SaleMessages.AlreadyCancelled));
        }

        if (sale.Version != command.ExpectedVersion)
        {
            return Result.Failure<ReturnResult>(new Conflict());
        }

        // 008: solo se cancelan ventas del turno abierto actual (incluye rechazar las anteriores a 0.6.0, sin turno).
        // 012: con Turnos sin licencia se omite esta regla y la de efectivo (no hay turno que controlar).
        if (_license?.IsModuleActive(LicensedModule.CashShifts) != false)
        {
            var shift = await _shifts.GetOpenAsync(CashRegister.Default, cancellationToken);
            if (shift is null || sale.CashShiftId != shift.Id)
            {
                return Result.Failure<ReturnResult>(new InvalidState(CashShiftMessages.SaleFromClosedShift));
            }

            var saleCash = await _sales.GetCashAppliedAsync(sale.Id, cancellationToken);
            if (saleCash > 0)
            {
                var expected = shift.ExpectedCash(await _sales.GetShiftTotalsAsync(shift.Id, cancellationToken));
                if (!CashShiftMath.CanRefund(expected, saleCash))
                {
                    // Nunca se revela el monto esperado, a ningún rol (clarificación 1).
                    LogInsufficientCash(sale.Id, shift.Id);
                    return Result.Failure<ReturnResult>(new InsufficientCash(null));
                }
            }
        }

        // 014: una venta a crédito nunca se cancela sin ajustar su saldo. Esta ruta no registra
        // devoluciones ni reintegros, así que no puede devolver efectivo por lo abonado de más.
        var isCredit = sale.Payments.Any(p => p.Method == PaymentMethod.OnAccount);
        var remaining = sale.TotalCents - sale.ReturnedCents;
        if (isCredit)
        {
            var preview = await Settlement().PreviewAsync(sale.Id, remaining, cancellationToken);
            if (preview is { CashRefundCents: > 0 })
            {
                LogCreditCashRefund(sale.Id, preview.CashRefundCents);
                return Result.Failure<ReturnResult>(new InvalidState(ReceivableMessages.CashRefundNeedsReturns));
            }
        }

        var reason = command.Reason.Trim();
        sale.Cancel(reason, _clock.UtcNow, _currentUser.UserId);
        if (_couponRelease is not null)
        {
            // 015: la cancelación completa devuelve el uso del cupón (FR-014).
            await _couponRelease.ReleaseForCancelledSaleAsync(sale, cancellationToken);
        }

        if (isCredit && remaining > 0)
        {
            // Sin registro de devolución, el origen de los movimientos de la cuenta es la propia venta.
            await Settlement().ApplyAsync(sale.Id, sale.Id, remaining, closesSale: true, cancellationToken);
        }

        var inventoryActive = _license?.IsModuleActive(LicensedModule.Inventory) != false;
        var withMovement = inventoryActive
            ? sale.Lines.Where(l => l.SaleMovementId is not null).ToList()
            : [];
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
            return Result.Failure<ReturnResult>(new Conflict());
        }

        await transaction.CommitAsync(cancellationToken);
        LogCancelled(sale.Id, sale.Folio);
        return Result.Success(ReturnResult.Basic(sale.TotalCents));
    }

    private CreditSettlementService Settlement() =>
        _creditSettlement ?? throw new InvalidOperationException("Falta el servicio de liquidación de ventas a crédito.");

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cancelación básica de venta a crédito rechazada: requiere reintegro en efectivo. SaleId={SaleId} ReintegroCents={CashRefundCents}")]
    private partial void LogCreditCashRefund(Guid saleId, long cashRefundCents);

    [LoggerMessage(Level = LogLevel.Information, Message = "Venta cancelada. SaleId={SaleId} Folio={Folio}")]
    private partial void LogCancelled(Guid saleId, string folio);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cancelación de venta rechazada por efectivo insuficiente. SaleId={SaleId} ShiftId={ShiftId}")]
    private partial void LogInsufficientCash(Guid saleId, Guid shiftId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cancelación de venta rechazada por conflicto. SaleId={SaleId}")]
    private partial void LogConflict(Guid saleId);
}
