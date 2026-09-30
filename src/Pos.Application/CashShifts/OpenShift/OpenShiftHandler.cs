using FluentValidation;
using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Printing.Ticket;
using Pos.Application.Products;
using Pos.Application.Users.Access;
using Pos.Domain.CashShifts;
using Pos.Domain.Common;
using Pos.Domain.Users;

namespace Pos.Application.CashShifts.OpenShift;

/// <summary>
/// Abre el turno de la caja en una sola transacción (FR-026): número consecutivo, fondo inicial y
/// bitácora. Un segundo turno abierto se rechaza por verificación y por el índice único (FR-004).
/// </summary>
public sealed partial class OpenShiftHandler
{
    private readonly IAccessControl _access;
    private readonly ICashShiftRepository _shifts;
    private readonly IAuditLog _audit;
    private readonly IWriteTransactions _transactions;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<OpenShiftCommand> _validator;
    private readonly ShiftGuard _guard;
    private readonly ILogger<OpenShiftHandler> _logger;

    public OpenShiftHandler(
        IAccessControl access,
        ICashShiftRepository shifts,
        IAuditLog audit,
        IWriteTransactions transactions,
        IClock clock,
        ICurrentUser currentUser,
        IValidator<OpenShiftCommand> validator,
        ShiftGuard guard,
        ILogger<OpenShiftHandler> logger)
    {
        _access = access;
        _shifts = shifts;
        _audit = audit;
        _transactions = transactions;
        _clock = clock;
        _currentUser = currentUser;
        _validator = validator;
        _guard = guard;
        _logger = logger;
    }

    public async Task<Result<CurrentShiftSummary>> HandleAsync(OpenShiftCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var access = await _access.CheckAsync(Permission.OperateShift, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<CurrentShiftSummary>(access.Error!);
        }

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<CurrentShiftSummary>(ProductRules.ToError(validation));
        }

        await using var transaction = await _transactions.BeginAsync(cancellationToken);

        if (await _shifts.GetOpenAsync(CashRegister.Default, cancellationToken) is not null)
        {
            LogRejected(_currentUser.UserId);
            return Result.Failure<CurrentShiftSummary>(new ShiftAlreadyOpen());
        }

        var floatMoney = Money.FromCents(command.OpeningFloatCents);
        var shift = CashShift.Open(await _shifts.NextNumberAsync(cancellationToken), floatMoney, _currentUser.UserId, _clock.UtcNow);
        _shifts.Add(shift);
        _audit.Add(
            AuditActions.ShiftOpened,
            AuditActions.CashShiftEntity,
            shift.Id,
            $"Turno {shift.Folio}. Fondo {TicketBuilder.FormatMoney(shift.OpeningFloatCents)}");

        var outcome = await _shifts.SaveChangesAsync(cancellationToken);
        if (outcome.Status != SaveStatus.Saved)
        {
            LogRejected(_currentUser.UserId);
            return Result.Failure<CurrentShiftSummary>(outcome.DuplicateField == CashShiftFields.OpenPerRegister
                ? new ShiftAlreadyOpen()
                : new Conflict());
        }

        await transaction.CommitAsync(cancellationToken);
        LogOpened(shift.Id, shift.Folio, _currentUser.UserId, shift.OpeningFloatCents);

        return Result.Success(new CurrentShiftSummary(
            shift.Id,
            shift.Version,
            shift.Folio,
            shift.OpenedAt,
            shift.OpenedBy,
            await _guard.NameOfAsync(shift.OpenedBy, cancellationToken),
            IsMine: true,
            SalesCount: 0,
            TotalSoldCents: 0));
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Turno abierto. ShiftId={ShiftId} Folio={Folio} UserId={UserId} FondoCents={OpeningFloatCents}")]
    private partial void LogOpened(Guid shiftId, string folio, Guid userId, long openingFloatCents);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Apertura de turno rechazada: ya hay un turno abierto. UserId={UserId}")]
    private partial void LogRejected(Guid userId);
}
