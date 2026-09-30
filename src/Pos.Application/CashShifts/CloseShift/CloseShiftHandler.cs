using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Printing.Ticket;
using Pos.Application.Products;
using Pos.Application.Sales;
using Pos.Application.Users.Access;
using Pos.Domain.CashShifts;
using Pos.Domain.Common;
using Pos.Domain.Users;

namespace Pos.Application.CashShifts.CloseShift;

/// <summary>
/// Segundo paso del cierre: recalcula todo dentro de la transacción, exige comentario si hay
/// diferencia y deja el turno cerrado con su instantánea inmutable (research §8 y §10). Una sola
/// entrada de cierre en la bitácora: <c>SHIFT_CLOSED</c> o, si lo cierra otro usuario,
/// <c>SHIFT_CLOSED_BY_ADMIN</c>.
/// </summary>
public sealed partial class CloseShiftHandler
{
    private readonly IAccessControl _access;
    private readonly ICurrentUser _currentUser;
    private readonly ICashShiftRepository _shifts;
    private readonly ISaleRepository _sales;
    private readonly ISaleDraftStore _drafts;
    private readonly ShiftGuard _guard;
    private readonly IAuditLog _audit;
    private readonly IWriteTransactions _transactions;
    private readonly IClock _clock;
    private readonly ILogger<CloseShiftHandler> _logger;

    public CloseShiftHandler(
        IAccessControl access,
        ICurrentUser currentUser,
        ICashShiftRepository shifts,
        ISaleRepository sales,
        ISaleDraftStore drafts,
        ShiftGuard guard,
        IAuditLog audit,
        IWriteTransactions transactions,
        IClock clock,
        ILogger<CloseShiftHandler> logger)
    {
        _access = access;
        _currentUser = currentUser;
        _shifts = shifts;
        _sales = sales;
        _drafts = drafts;
        _guard = guard;
        _audit = audit;
        _transactions = transactions;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<ClosedShift>> HandleAsync(CloseShiftCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var access = await _access.CheckAsync(Permission.OperateShift, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<ClosedShift>(access.Error!);
        }

        var errors = new List<FieldError>();
        if (command.CountedCents is < 0 or > Money.MaxCents)
        {
            errors.Add(new FieldError(CashShiftFields.Counted, CashShiftMessages.CountedInvalid));
        }

        if (command.Comment is { } raw && raw.Trim().Length > CashShift.CommentMaxLength)
        {
            errors.Add(new FieldError(CashShiftFields.Comment, CashShiftMessages.CommentTooLong));
        }

        if (errors.Count > 0)
        {
            return Result.Failure<ClosedShift>(new ValidationFailed(errors));
        }

        await using var transaction = await _transactions.BeginAsync(cancellationToken);

        var shift = await _shifts.GetAsync(command.ShiftId, cancellationToken);
        if (shift is null)
        {
            return Result.Failure<ClosedShift>(new NotFound());
        }

        if (shift.Status != CashShiftStatus.Open)
        {
            return Result.Failure<ClosedShift>(new ShiftClosed());
        }

        var isOwn = shift.OpenedBy == _currentUser.UserId;
        if (!isOwn)
        {
            var manage = await _access.CheckAsync(Permission.ManageShifts, cancellationToken);
            if (!manage.Allowed)
            {
                return Result.Failure<ClosedShift>(manage.Error!);
            }
        }

        if (shift.Version != command.ExpectedVersion)
        {
            return Result.Failure<ClosedShift>(new Conflict());
        }

        if (await _guard.CheckHeldSaleAsync(shift, command.DiscardHeldSale, cancellationToken) is { } blocked)
        {
            return Result.Failure<ClosedShift>(blocked);
        }

        var ownerName = await _guard.NameOfAsync(shift.OpenedBy, cancellationToken);
        if (!isOwn && command.DiscardHeldSale && await _drafts.RemoveForAsync(shift.OpenedBy, cancellationToken))
        {
            _audit.Add(
                AuditActions.HeldSaleDiscarded,
                AuditActions.UserEntity,
                shift.OpenedBy,
                $"Al cerrar el turno {shift.Folio}");
        }

        var totals = await _sales.GetShiftTotalsAsync(shift.Id, cancellationToken);
        var expected = shift.ExpectedCash(totals);
        if (expected != command.ShownExpectedCents)
        {
            LogChanged(shift.Id, _currentUser.UserId);
            return Result.Failure<ClosedShift>(new ShiftChanged());
        }

        var difference = command.CountedCents - expected;
        if (difference != 0 && string.IsNullOrWhiteSpace(command.Comment))
        {
            return Result.Failure<ClosedShift>(new ValidationFailed(
                [new FieldError(CashShiftFields.Comment, CashShiftMessages.CommentRequired)]));
        }

        try
        {
            shift.Close(totals, Money.FromCents(command.CountedCents), command.Comment, _currentUser.UserId, _clock.UtcNow);
        }
        catch (DomainException ex)
        {
            LogRejected(ex, shift.Id, _currentUser.UserId);
            return Result.Failure<ClosedShift>(new ValidationFailed([new FieldError(CashShiftFields.Comment, ex.Message)]));
        }

        var summary = $"Turno {shift.Folio}. Esperado {TicketBuilder.FormatMoney(expected)}, "
            + $"contado {TicketBuilder.FormatMoney(command.CountedCents)}, diferencia {TicketBuilder.FormatMoney(difference)}";
        _audit.Add(
            isOwn ? AuditActions.ShiftClosed : AuditActions.ShiftClosedByAdmin,
            AuditActions.CashShiftEntity,
            shift.Id,
            isOwn ? summary : $"{summary}. Dueño: {ownerName}");

        var outcome = await _shifts.SaveChangesAsync(cancellationToken);
        if (outcome.Status != SaveStatus.Saved)
        {
            return Result.Failure<ClosedShift>(new Conflict());
        }

        await transaction.CommitAsync(cancellationToken);
        LogClosed(shift.Id, shift.Folio, _currentUser.UserId, expected, command.CountedCents, difference);
        return Result.Success(new ClosedShift(shift.Id, shift.Folio));
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Turno cerrado. ShiftId={ShiftId} Folio={Folio} UserId={UserId} EsperadoCents={ExpectedCents} ContadoCents={CountedCents} DiferenciaCents={DifferenceCents}")]
    private partial void LogClosed(Guid shiftId, string folio, Guid userId, long expectedCents, long countedCents, long differenceCents);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cierre de turno rechazado: el esperado cambió. ShiftId={ShiftId} UserId={UserId}")]
    private partial void LogChanged(Guid shiftId, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cierre de turno rechazado por reglas de dominio. ShiftId={ShiftId} UserId={UserId}")]
    private partial void LogRejected(Exception exception, Guid shiftId, Guid userId);
}
