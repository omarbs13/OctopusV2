using FluentValidation;
using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Customers;
using Pos.Application.Printing.Ticket;
using Pos.Application.Products;
using Pos.Application.Users.Access;
using Pos.Domain.Receivables;
using Pos.Domain.Sales;
using Pos.Domain.Users;

namespace Pos.Application.Receivables.VoidCustomerPayment;

/// <summary>
/// Anula un abono en una transacción, en el orden de contracts/application-ports.md: licencia →
/// permiso → motivo → abono vigente → sin devolución posterior en sus cuentas (research §7) → turno
/// abierto → efectivo suficiente si fue en efectivo (sin revelar montos) → consumir la concesión →
/// <c>PAYMENT_VOID</c> y estados → anular → bitácora → guardar. Sin licencia de Turnos se omiten turno
/// y efectivo y la anulación queda sin turno.
/// </summary>
public sealed partial class VoidCustomerPaymentHandler
{
    private readonly IAccessControl _access;
    private readonly IAuthorizationGrants _grants;
    private readonly ICustomerRepository _customers;
    private readonly IReceivableRepository _receivables;
    private readonly ICustomerPaymentRepository _payments;
    private readonly PaymentShiftGate _shiftGate;
    private readonly IAuditLog _audit;
    private readonly IWriteTransactions _transactions;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<VoidCustomerPaymentCommand> _validator;
    private readonly ILogger<VoidCustomerPaymentHandler> _logger;

    public VoidCustomerPaymentHandler(
        IAccessControl access,
        IAuthorizationGrants grants,
        ICustomerRepository customers,
        IReceivableRepository receivables,
        ICustomerPaymentRepository payments,
        PaymentShiftGate shiftGate,
        IAuditLog audit,
        IWriteTransactions transactions,
        IClock clock,
        ICurrentUser currentUser,
        IValidator<VoidCustomerPaymentCommand> validator,
        ILogger<VoidCustomerPaymentHandler> logger)
    {
        _access = access;
        _grants = grants;
        _customers = customers;
        _receivables = receivables;
        _payments = payments;
        _shiftGate = shiftGate;
        _audit = audit;
        _transactions = transactions;
        _clock = clock;
        _currentUser = currentUser;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(VoidCustomerPaymentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var access = await _access.CheckAsync(Permission.RegisterCustomerPayments, cancellationToken);
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

        var payment = await _payments.GetAsync(command.PaymentId, cancellationToken);
        if (payment is null)
        {
            return Result.Failure(new NotFound());
        }

        if (payment.Status != CustomerPaymentStatus.Active)
        {
            return Result.Failure(new InvalidState(ReceivableMessages.AlreadyVoided));
        }

        // Lo abonado a una venta que después se devolvió ya se reaplicó o se reintegró (research §7).
        var entries = await _receivables.GetEntriesByPaymentAsync(payment.Id, cancellationToken);
        var receivableIds = entries.Select(e => e.ReceivableId).Distinct().ToList();
        if (await _receivables.HasReturnAfterAsync(receivableIds, payment.CreatedAt, cancellationToken))
        {
            LogReturnedAfter(payment.Id, payment.Folio);
            return Result.Failure(new InvalidState(ReceivableMessages.ReturnedAfter));
        }

        var shift = await _shiftGate.ResolveAsync(payment.Method == PaymentMethod.Cash ? payment.AmountCents : 0, cancellationToken);
        if (!shift.IsSuccess)
        {
            // Nunca se revela el efectivo esperado, a ningún rol (008).
            LogShiftRejected(payment.Id, shift.Error.GetType().Name);
            return Result.Failure(shift.Error);
        }

        // La concesión se consume solo después de validar todo; se exige siempre, también al Administrador,
        // que se autoriza capturando su propia contraseña (research §7, como ApproveReturns en 013).
        var authorizedBy = command.AuthorizationGrantId is { } grantId
            ? _grants.TryConsume(grantId, Permission.VoidCustomerPayments, _currentUser.UserId)
            : null;
        if (authorizedBy is null)
        {
            if (command.AuthorizationGrantId is not null)
            {
                _audit.Add(
                    AuditActions.AdminAuthorizationDenied,
                    AuditActions.CustomerPaymentEntity,
                    payment.Id,
                    $"Permiso: {Permission.VoidCustomerPayments}. Concesión inválida o vencida. Abono {payment.Folio}");
                await _audit.SaveAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }

            LogNotAuthorized(payment.Id, _currentUser.UserId);
            return Result.Failure(new Forbidden(Permission.VoidCustomerPayments, CanBeAuthorized: true));
        }

        foreach (var receivable in await _receivables.GetManyAsync(receivableIds, cancellationToken))
        {
            receivable.RevertPayment(payment.Id);
        }

        var reason = command.Reason.Trim();
        payment.Void(reason, _currentUser.UserId, authorizedBy.Value, shift.Value?.Id, _clock.UtcNow);
        var customer = await _customers.GetAsync(payment.CustomerId, cancellationToken);
        _audit.Add(
            AuditActions.CustomerPaymentVoided,
            AuditActions.CustomerPaymentEntity,
            payment.Id,
            $"Abono {payment.Folio}. Cliente: {customer?.Name}. Monto {TicketBuilder.FormatMoney(payment.AmountCents)}. Motivo: {reason}",
            authorizedBy);

        var outcome = await _payments.SaveChangesAsync(cancellationToken);
        if (outcome.Status != SaveStatus.Saved)
        {
            return Result.Failure(new Conflict());
        }

        await transaction.CommitAsync(cancellationToken);
        LogVoided(payment.Id, payment.Folio, payment.CustomerId, _currentUser.UserId, authorizedBy.Value, payment.AmountCents);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Abono anulado. PaymentId={PaymentId} Folio={Folio} CustomerId={CustomerId} UserId={UserId} AutorizadoPor={AuthorizedBy} AmountCents={AmountCents}")]
    private partial void LogVoided(Guid paymentId, string folio, Guid customerId, Guid userId, Guid authorizedBy, long amountCents);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Anulación de abono rechazada: sin autorización de Administrador. PaymentId={PaymentId} UserId={UserId}")]
    private partial void LogNotAuthorized(Guid paymentId, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Anulación de abono rechazada: la venta se devolvió después. PaymentId={PaymentId} Folio={Folio}")]
    private partial void LogReturnedAfter(Guid paymentId, string folio);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Anulación de abono rechazada por el turno o el efectivo. PaymentId={PaymentId} Motivo={Reason}")]
    private partial void LogShiftRejected(Guid paymentId, string reason);
}
