using FluentValidation;
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

namespace Pos.Application.CashShifts.RegisterCashMovement;

/// <summary>
/// Registra un ingreso o un retiro de efectivo en una sola transacción (FR-026) con su bitácora. El
/// efectivo esperado se calcula dentro de la transacción, nunca se acumula (research §3).
/// </summary>
public sealed partial class RegisterCashMovementHandler
{
    private readonly IAccessControl _access;
    private readonly ICashShiftRepository _shifts;
    private readonly ISaleRepository _sales;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLog _audit;
    private readonly IWriteTransactions _transactions;
    private readonly IValidator<RegisterCashMovementCommand> _validator;
    private readonly ILogger<RegisterCashMovementHandler> _logger;

    public RegisterCashMovementHandler(
        IAccessControl access,
        ICashShiftRepository shifts,
        ISaleRepository sales,
        ICurrentUser currentUser,
        IAuditLog audit,
        IWriteTransactions transactions,
        IValidator<RegisterCashMovementCommand> validator,
        ILogger<RegisterCashMovementHandler> logger)
    {
        _access = access;
        _shifts = shifts;
        _sales = sales;
        _currentUser = currentUser;
        _audit = audit;
        _transactions = transactions;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<RegisteredMovement>> HandleAsync(RegisterCashMovementCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var isWithdrawal = command.Type == CashMovementType.Out;
        var access = await _access.CheckAsync(
            isWithdrawal ? Permission.WithdrawCash : Permission.OperateShift,
            isWithdrawal ? command.AuthorizationGrantId : null,
            cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<RegisteredMovement>(access.Error!);
        }

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<RegisteredMovement>(ProductRules.ToError(validation));
        }

        await using var transaction = await _transactions.BeginAsync(cancellationToken);

        var shift = await _shifts.GetAsync(command.ShiftId, cancellationToken);
        if (shift is null)
        {
            return Result.Failure<RegisteredMovement>(new NotFound());
        }

        if (shift.Status != CashShiftStatus.Open)
        {
            return Result.Failure<RegisteredMovement>(new ShiftClosed());
        }

        if (shift.OpenedBy != _currentUser.UserId)
        {
            var manage = await _access.CheckAsync(Permission.ManageShifts, cancellationToken);
            if (!manage.Allowed)
            {
                return Result.Failure<RegisteredMovement>(manage.Error!);
            }
        }

        var amount = Money.FromCents(command.AmountCents);
        var reason = command.Reason.Trim();
        CashMovement movement;
        try
        {
            if (isWithdrawal)
            {
                var expected = shift.ExpectedCash(await _sales.GetShiftTotalsAsync(shift.Id, cancellationToken));
                movement = shift.RecordWithdrawal(amount, reason, expected, access.AuthorizedBy);
            }
            else
            {
                movement = shift.RecordDeposit(amount, reason);
            }
        }
        catch (InsufficientCashException ex)
        {
            // Solo quien administra turnos ve el monto disponible (FR-010).
            var canSeeAmount = await _access.HasAsync(Permission.ManageShifts, cancellationToken);
            LogInsufficient(shift.Id, _currentUser.UserId, command.AmountCents);
            return Result.Failure<RegisteredMovement>(new InsufficientCash(canSeeAmount ? ex.AvailableCents : null));
        }
        catch (DomainException ex)
        {
            LogRejected(ex, shift.Id, _currentUser.UserId);
            return Result.Failure<RegisteredMovement>(new ValidationFailed([new FieldError(CashShiftFields.Amount, ex.Message)]));
        }

        _shifts.AddMovement(movement);
        var folio = ShiftFolio.FormatMovement(shift.Number, movement.Sequence);
        _audit.Add(
            isWithdrawal ? AuditActions.CashWithdrawal : AuditActions.CashDeposit,
            AuditActions.CashShiftEntity,
            shift.Id,
            $"Turno {folio}. {TicketBuilder.FormatMoney(movement.AmountCents)}. Motivo: {movement.Reason}",
            movement.AuthorizedBy);

        var outcome = await _shifts.SaveChangesAsync(cancellationToken);
        if (outcome.Status != SaveStatus.Saved)
        {
            return Result.Failure<RegisteredMovement>(new Conflict());
        }

        await transaction.CommitAsync(cancellationToken);
        LogRegistered(shift.Id, folio, movement.Type, movement.AmountCents, _currentUser.UserId);
        return Result.Success(new RegisteredMovement(movement.Id, folio));
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Movimiento de efectivo registrado. ShiftId={ShiftId} Folio={Folio} Tipo={Type} MontoCents={AmountCents} UserId={UserId}")]
    private partial void LogRegistered(Guid shiftId, string folio, CashMovementType type, long amountCents, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Retiro rechazado por efectivo insuficiente. ShiftId={ShiftId} UserId={UserId} MontoCents={AmountCents}")]
    private partial void LogInsufficient(Guid shiftId, Guid userId, long amountCents);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Movimiento de efectivo rechazado por reglas de dominio. ShiftId={ShiftId} UserId={UserId}")]
    private partial void LogRejected(Exception exception, Guid shiftId, Guid userId);
}
