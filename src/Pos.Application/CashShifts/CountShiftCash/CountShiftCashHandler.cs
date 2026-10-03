using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Printing.Ticket;
using Pos.Application.Sales;
using Pos.Application.Users.Access;
using Pos.Domain.CashShifts;
using Pos.Domain.Common;
using Pos.Domain.Users;

namespace Pos.Application.CashShifts.CountShiftCash;

/// <summary>
/// Primer paso del arqueo ciego (research §8): recibe el conteo y solo entonces revela esperado,
/// diferencia y totales de tarjeta y transferencia. Cada conteo queda en la bitácora. Rechaza con
/// <see cref="SaleInProgress"/> o <see cref="HeldSaleWillBeDiscarded"/> antes de revelar cifras.
/// </summary>
public sealed partial class CountShiftCashHandler
{
    private readonly IAccessControl _access;
    private readonly ICurrentUser _currentUser;
    private readonly ICashShiftRepository _shifts;
    private readonly ISaleRepository _sales;
    private readonly ShiftGuard _guard;
    private readonly IAuditLog _audit;
    private readonly IWriteTransactions _transactions;
    private readonly ILogger<CountShiftCashHandler> _logger;

    public CountShiftCashHandler(
        IAccessControl access,
        ICurrentUser currentUser,
        ICashShiftRepository shifts,
        ISaleRepository sales,
        ShiftGuard guard,
        IAuditLog audit,
        IWriteTransactions transactions,
        ILogger<CountShiftCashHandler> logger)
    {
        _access = access;
        _currentUser = currentUser;
        _shifts = shifts;
        _sales = sales;
        _guard = guard;
        _audit = audit;
        _transactions = transactions;
        _logger = logger;
    }

    public async Task<Result<ShiftCountResult>> HandleAsync(CountShiftCashCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // 025, FR-030a: el turno abierto se consulta, cuenta y cierra aunque Turnos y arqueo no esté activo o el
        // sistema esté bloqueado (Principio I); solo se exigen sesión y rol.
        var access = await _access.CheckToFinishAsync(Permission.OperateShift, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<ShiftCountResult>(access.Error!);
        }

        if (command.CountedCents is < 0 or > Money.MaxCents)
        {
            return Result.Failure<ShiftCountResult>(new ValidationFailed(
                [new FieldError(CashShiftFields.Counted, CashShiftMessages.CountedInvalid)]));
        }

        await using var transaction = await _transactions.BeginAsync(cancellationToken);

        var shift = await _shifts.GetAsync(command.ShiftId, cancellationToken);
        if (shift is null)
        {
            return Result.Failure<ShiftCountResult>(new NotFound());
        }

        if (shift.Status != CashShiftStatus.Open)
        {
            return Result.Failure<ShiftCountResult>(new ShiftClosed());
        }

        if (shift.OpenedBy != _currentUser.UserId)
        {
            var manage = await _access.CheckToFinishAsync(Permission.ManageShifts, cancellationToken);
            if (!manage.Allowed)
            {
                return Result.Failure<ShiftCountResult>(manage.Error!);
            }
        }

        if (await _guard.CheckHeldSaleAsync(shift, command.DiscardHeldSale, cancellationToken) is { } blocked)
        {
            return Result.Failure<ShiftCountResult>(blocked);
        }

        var totals = await _sales.GetShiftTotalsAsync(shift.Id, cancellationToken);
        var expected = shift.ExpectedCash(totals);
        var difference = command.CountedCents - expected;

        _audit.Add(
            AuditActions.ShiftCashCounted,
            AuditActions.CashShiftEntity,
            shift.Id,
            $"Turno {shift.Folio}. Contado {TicketBuilder.FormatMoney(command.CountedCents)}");
        await _audit.SaveAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        LogCounted(shift.Id, shift.Folio, _currentUser.UserId, command.CountedCents);
        return Result.Success(new ShiftCountResult(
            expected,
            command.CountedCents,
            difference,
            CashDifference.FromSigned(difference).Kind,
            totals.CardCents,
            totals.TransferCents,
            shift.Version));
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Conteo de caja. ShiftId={ShiftId} Folio={Folio} UserId={UserId} ContadoCents={CountedCents}")]
    private partial void LogCounted(Guid shiftId, string folio, Guid userId, long countedCents);
}
