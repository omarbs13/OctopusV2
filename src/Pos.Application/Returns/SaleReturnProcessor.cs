using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.CreditNotes;
using Pos.Application.Inventory;
using Pos.Application.Licensing;
using Pos.Application.Printing.Ticket;
using Pos.Application.Products;
using Pos.Application.Sales;
using Pos.Application.Users.Access;
using Pos.Domain.Common;
using Pos.Domain.CreditNotes;
using Pos.Domain.Licensing;
using Pos.Domain.Returns;
using Pos.Domain.Sales;
using Pos.Domain.Users;

namespace Pos.Application.Returns;

/// <summary>
/// Cancelación o devolución parcial que se pide al procesador. <c>Lines</c> nulo = toda la venta
/// (cancelación completa). <c>AuthorizationGrantId</c> es la concesión de <c>ApproveReturns</c> (007).
/// </summary>
public sealed record ReturnRequest(
    Guid SaleId,
    int ExpectedVersion,
    ReturnKind Kind,
    IReadOnlyList<ReturnLineRequest>? Lines,
    string Reason,
    ReturnCompensation Compensation,
    Guid? AuthorizationGrantId);

/// <summary>
/// Núcleo compartido por <c>CancelSale</c> y <c>ReturnSaleItems</c> (contracts/application-ports.md).
/// Orden dentro de la transacción: licencia → permiso → entrada → venta → versión y estado → plazo →
/// cantidades → turno y efectivo → consumir la concesión → devolución, inventario, reintegros o nota →
/// bitácora → guardar → confirmar. Una falla no deja nada a medias (FR-011).
/// </summary>
public sealed partial class SaleReturnProcessor
{
    private readonly IAccessControl _access;
    private readonly IAuthorizationGrants _grants;
    private readonly ISaleRepository _sales;
    private readonly IReturnRepository _returns;
    private readonly ICreditNoteRepository _creditNotes;
    private readonly IInventoryRepository _inventory;
    private readonly ReturnCashGate _cashGate;
    private readonly IReturnsSettingsStore _settings;
    private readonly IAuditLog _audit;
    private readonly IWriteTransactions _transactions;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<SaleReturnProcessor> _logger;
    private readonly ILicenseState? _license;

    public SaleReturnProcessor(
        IAccessControl access,
        IAuthorizationGrants grants,
        ISaleRepository sales,
        IReturnRepository returns,
        ICreditNoteRepository creditNotes,
        IInventoryRepository inventory,
        ReturnCashGate cashGate,
        IReturnsSettingsStore settings,
        IAuditLog audit,
        IWriteTransactions transactions,
        IClock clock,
        ICurrentUser currentUser,
        ILogger<SaleReturnProcessor> logger,
        ILicenseState? license = null)
    {
        _access = access;
        _grants = grants;
        _sales = sales;
        _returns = returns;
        _creditNotes = creditNotes;
        _inventory = inventory;
        _cashGate = cashGate;
        _settings = settings;
        _audit = audit;
        _transactions = transactions;
        _clock = clock;
        _currentUser = currentUser;
        _logger = logger;
        _license = license;
    }

    public async Task<Result<ReturnResult>> ProcessAsync(ReturnRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var access = await _access.CheckAsync(Permission.ProcessReturns, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<ReturnResult>(access.Error!);
        }

        await using var transaction = await _transactions.BeginAsync(cancellationToken);

        var sale = await _sales.GetAsync(request.SaleId, cancellationToken);
        if (sale is null)
        {
            return Result.Failure<ReturnResult>(new NotFound());
        }

        // Un cajero solo cancela o devuelve sus propias ventas (007, FR-026).
        if (await SaleAccess.CheckOwnershipAsync(_access, _currentUser, sale.CreatedBy, cancellationToken) is { } forbidden)
        {
            return Result.Failure<ReturnResult>(forbidden);
        }

        if (sale.Status != SaleStatus.Completed)
        {
            return Result.Failure<ReturnResult>(new InvalidState(SaleMessages.AlreadyCancelled));
        }

        if (sale.Version != request.ExpectedVersion)
        {
            return Result.Failure<ReturnResult>(new Conflict());
        }

        var isCancellation = request.Kind == ReturnKind.Cancellation;
        if (isCancellation && sale.ReturnedCents > 0)
        {
            return Result.Failure<ReturnResult>(new InvalidState(ReturnMessages.PartiallyReturned));
        }

        var windowDays = _settings.Load().ReturnWindowDays;
        if (_clock.UtcNow - sale.CreatedAt > TimeSpan.FromDays(windowDays))
        {
            LogWindowExpired(sale.Id, windowDays);
            return Result.Failure<ReturnResult>(new ReturnWindowExpired(windowDays));
        }

        var lines = request.Lines ?? ReturnPlanner.AllAvailable(sale);
        var returnedByPayment = await _returns.GetReturnedByPaymentAsync(sale.Id, cancellationToken);
        ReturnPlan plan;
        try
        {
            plan = ReturnPlanner.Build(sale, lines, returnedByPayment);
        }
        catch (DomainException ex)
        {
            LogRejected(ex, sale.Id);
            return Result.Failure<ReturnResult>(new NothingToReturn());
        }

        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length == 0 || reason.Length > SaleReturn.ReasonMaxLength)
        {
            return Result.Failure<ReturnResult>(new ValidationFailed(
                [new FieldError(ReturnFields.Reason, reason.Length == 0 ? ReturnMessages.ReasonRequired : ReturnMessages.ReasonTooLong)]));
        }

        var refund = request.Compensation == ReturnCompensation.Refund;
        var gate = await _cashGate.ResolveAsync(refund ? plan.CashCents : 0, cancellationToken);
        if (!gate.IsSuccess)
        {
            if (gate.Error is InsufficientCash)
            {
                // Nunca se revela el monto esperado, a ningún rol (008).
                LogInsufficientCash(sale.Id);
            }

            return Result.Failure<ReturnResult>(gate.Error);
        }

        // La concesión se consume solo después de validar todo; una validación fallida no la gasta (research §7).
        // Se exige siempre, también al Administrador: él se autoriza capturando su propia contraseña.
        var authorizedBy = request.AuthorizationGrantId is { } grantId
            ? _grants.TryConsume(grantId, Permission.ApproveReturns, _currentUser.UserId)
            : null;
        if (authorizedBy is null)
        {
            LogNotAuthorized(sale.Id, _currentUser.UserId);
            return Result.Failure<ReturnResult>(new Forbidden(Permission.ApproveReturns, CanBeAuthorized: true));
        }

        try
        {
            return await ApplyAsync(request, sale, plan, reason, gate.Value?.Id, authorizedBy.Value, transaction, cancellationToken);
        }
        catch (DomainException ex)
        {
            LogRejected(ex, sale.Id);
            return Result.Failure<ReturnResult>(new ValidationFailed([new FieldError(ReturnFields.Lines, ex.Message)]));
        }
    }

    private async Task<Result<ReturnResult>> ApplyAsync(
        ReturnRequest request,
        Sale sale,
        ReturnPlan plan,
        string reason,
        Guid? cashShiftId,
        Guid authorizedBy,
        IWriteTransaction transaction,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var isCancellation = request.Kind == ReturnKind.Cancellation;
        var returnId = Guid.CreateVersion7();
        var number = await _returns.NextNumberAsync(cancellationToken);

        // Estado de la venta y movimientos de inventario (solo productos con inventario y módulo con licencia).
        if (isCancellation)
        {
            sale.EnsureCanCancelInFull();
            sale.Cancel(reason, now, _currentUser.UserId);
        }
        else
        {
            sale.ApplyReturn(plan.Lines.Select(l => new ReturnLineRequest(l.SaleLineId, l.QuantityThousandths)));
        }

        var movementByLine = await RestoreInventoryAsync(sale, plan, isCancellation, cancellationToken);
        var saleReturnLines = plan.Lines
            .Select(l => SaleReturnLine.Create(l.SaleLineId, l.QuantityThousandths, l.AmountCents, movementByLine.TryGetValue(l.SaleLineId, out var movementId) ? movementId : null))
            .ToList();

        var refunds = new List<SaleReturnRefund>();
        CreditNote? note = null;
        if (request.Compensation == ReturnCompensation.Refund)
        {
            foreach (var share in plan.Shares)
            {
                refunds.Add(SaleReturnRefund.Create(share.Payment.Id, share.Payment.Method, share.AmountCents));
                if (share.Payment.Method == PaymentMethod.CreditNote)
                {
                    await RestoreCreditNoteAsync(sale, share, returnId, cancellationToken);
                }
            }
        }
        else
        {
            note = CreditNote.Issue(await _creditNotes.NextNumberAsync(cancellationToken), returnId, plan.TotalCents);
            _creditNotes.Add(note);
            _creditNotes.AddMovement(note.RecordIssue(sale.Id));
        }

        var saleReturn = SaleReturn.Create(
            returnId,
            number,
            sale.Id,
            request.Kind,
            reason,
            authorizedBy,
            request.Compensation,
            cashShiftId,
            saleReturnLines,
            refunds,
            note?.Id);
        _returns.Add(saleReturn);

        var compensation = note is null
            ? "Reintegro"
            : $"Nota de crédito {note.Folio}";
        _audit.Add(
            isCancellation ? AuditActions.SaleCancelled : AuditActions.SaleReturned,
            AuditActions.SaleEntity,
            sale.Id,
            $"Folio {sale.Folio}. Devolución {saleReturn.Folio}. Motivo: {reason}. Monto {TicketBuilder.FormatMoney(plan.TotalCents)}. Compensación: {compensation}",
            authorizedBy);

        var outcome = await _returns.SaveChangesAsync(cancellationToken);
        if (outcome.Status != SaveStatus.Saved)
        {
            LogConflict(sale.Id);
            return Result.Failure<ReturnResult>(new Conflict());
        }

        await transaction.CommitAsync(cancellationToken);
        LogReturned(sale.Id, sale.Folio, saleReturn.Folio, _currentUser.UserId, plan.TotalCents, request.Compensation);
        return Result.Success(new ReturnResult(saleReturn.Id, saleReturn.Folio, plan.TotalCents, note?.Id, note?.Folio));
    }

    /// <summary>Regresa las existencias; devuelve el movimiento generado por línea.</summary>
    private async Task<Dictionary<Guid, Guid>> RestoreInventoryAsync(
        Sale sale,
        ReturnPlan plan,
        bool isCancellation,
        CancellationToken cancellationToken)
    {
        var movements = new Dictionary<Guid, Guid>();
        if (_license?.IsModuleActive(LicensedModule.Inventory) == false)
        {
            return movements;
        }

        var withMovement = plan.Lines
            .Select(l => (Amount: l, Line: sale.Lines.Single(x => x.Id == l.SaleLineId)))
            .Where(x => x.Line.SaleMovementId is not null)
            .ToList();
        if (withMovement.Count == 0)
        {
            return movements;
        }

        var stocks = (await _inventory.GetStocksAsync([.. withMovement.Select(x => x.Line.ProductId).Distinct()], cancellationToken))
            .ToDictionary(s => s.Key, s => s.Value);
        foreach (var (amount, line) in withMovement)
        {
            var stock = stocks[line.ProductId];
            var quantity = Quantity.FromThousandths(amount.QuantityThousandths);
            var movement = isCancellation
                ? stock.RecordSaleCancellation(quantity, sale.Folio)
                : stock.RecordSaleReturn(quantity, sale.Folio);
            _inventory.AddMovement(movement);
            movements[line.Id] = movement.Id;
            if (isCancellation)
            {
                sale.LinkCancellationMovement(line.Id, movement.Id);
            }
        }

        return movements;
    }

    /// <summary>Lo pagado con una nota vuelve como saldo a esa misma nota, nunca como efectivo (Historia 4, escenario 5).</summary>
    private async Task RestoreCreditNoteAsync(Sale sale, PaymentShare share, Guid returnId, CancellationToken cancellationToken)
    {
        var noteId = share.Payment.CreditNoteId ?? throw new DomainException("El pago con nota de crédito no tiene nota ligada.");
        var note = await _creditNotes.GetAsync(noteId, cancellationToken)
            ?? throw new DomainException("La nota de crédito del pago no existe.");
        var sequence = await _creditNotes.NextMovementSequenceAsync(note.Id, cancellationToken);
        _creditNotes.AddMovement(note.Restore(share.AmountCents, sequence, sale.Id, returnId));
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Devolución registrada. SaleId={SaleId} Folio={Folio} Devolucion={ReturnFolio} UserId={UserId} TotalCents={TotalCents} Compensacion={Compensation}")]
    private partial void LogReturned(Guid saleId, string folio, string returnFolio, Guid userId, long totalCents, ReturnCompensation compensation);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Devolución rechazada: sin autorización de Administrador. SaleId={SaleId} UserId={UserId}")]
    private partial void LogNotAuthorized(Guid saleId, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Devolución rechazada: fuera del plazo. SaleId={SaleId} Dias={Days}")]
    private partial void LogWindowExpired(Guid saleId, int days);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Devolución rechazada por efectivo insuficiente. SaleId={SaleId}")]
    private partial void LogInsufficientCash(Guid saleId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Devolución rechazada por reglas de dominio. SaleId={SaleId}")]
    private partial void LogRejected(Exception exception, Guid saleId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Devolución no guardada por conflicto. SaleId={SaleId}")]
    private partial void LogConflict(Guid saleId);
}
