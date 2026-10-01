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

namespace Pos.Application.Receivables.RegisterCustomerPayment;

/// <summary>
/// Registra un abono en una sola transacción, en el orden de research §5: licencia → permiso →
/// validación → turno → idempotencia por <c>RequestId</c> → cliente y saldo → reparto FIFO → abono con
/// folio y entradas <c>PAYMENT</c> → estados de las cuentas → bitácora → guardar. La transacción
/// serializada vuelve a leer el saldo, así que dos abonos simultáneos nunca lo dejan negativo.
/// </summary>
public sealed partial class RegisterCustomerPaymentHandler
{
    private const int MaxAttempts = 3;

    private readonly IAccessControl _access;
    private readonly ICustomerRepository _customers;
    private readonly IReceivableRepository _receivables;
    private readonly ICustomerPaymentRepository _payments;
    private readonly PaymentShiftGate _shiftGate;
    private readonly IAuditLog _audit;
    private readonly IWriteTransactions _transactions;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<RegisterCustomerPaymentCommand> _validator;
    private readonly ILogger<RegisterCustomerPaymentHandler> _logger;

    public RegisterCustomerPaymentHandler(
        IAccessControl access,
        ICustomerRepository customers,
        IReceivableRepository receivables,
        ICustomerPaymentRepository payments,
        PaymentShiftGate shiftGate,
        IAuditLog audit,
        IWriteTransactions transactions,
        ICurrentUser currentUser,
        IValidator<RegisterCustomerPaymentCommand> validator,
        ILogger<RegisterCustomerPaymentHandler> logger)
    {
        _access = access;
        _customers = customers;
        _receivables = receivables;
        _payments = payments;
        _shiftGate = shiftGate;
        _audit = audit;
        _transactions = transactions;
        _currentUser = currentUser;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<PaymentReceipt>> HandleAsync(RegisterCustomerPaymentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var access = await _access.CheckAsync(Permission.RegisterCustomerPayments, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<PaymentReceipt>(access.Error!);
        }

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<PaymentReceipt>(ProductRules.ToError(validation));
        }

        // Un folio duplicado solo ocurre si otro proceso escribió fuera de la transacción: se reintenta completo.
        for (var attempt = 1; ; attempt++)
        {
            var (result, retry) = await AttemptAsync(command, cancellationToken);
            if (!retry || attempt == MaxAttempts)
            {
                return result;
            }

            LogRetry(command.RequestId, attempt);
        }
    }

    private async Task<(Result<PaymentReceipt> Result, bool Retry)> AttemptAsync(RegisterCustomerPaymentCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await _transactions.BeginAsync(cancellationToken);

        var shift = await _shiftGate.ResolveAsync(0, cancellationToken);
        if (!shift.IsSuccess)
        {
            LogShiftRejected(command.CustomerId, shift.Error.GetType().Name);
            return (Result.Failure<PaymentReceipt>(shift.Error), false);
        }

        if (await _payments.FindByRequestAsync(command.RequestId, cancellationToken) is { } existing)
        {
            return (Result.Success(await ReceiptOfAsync(existing, cancellationToken)), false);
        }

        var customer = await _customers.GetAsync(command.CustomerId, cancellationToken);
        if (customer is null)
        {
            return (Result.Failure<PaymentReceipt>(new NotFound()), false);
        }

        var pending = await _receivables.GetPendingAsync(customer.Id, cancellationToken);
        var balance = pending.Sum(r => r.BalanceCents);
        if (command.AmountCents <= 0 || command.AmountCents > balance)
        {
            LogExceedsBalance(customer.Id, command.AmountCents, balance);
            return (Result.Failure<PaymentReceipt>(new PaymentExceedsBalance(balance)), false);
        }

        var allocations = PaymentAllocator.Allocate(
            command.AmountCents,
            pending.Select(r => new PendingBalance(r.Id, r.CreatedAt, r.BalanceCents)));
        var payment = CustomerPayment.Register(
            await _payments.NextNumberAsync(cancellationToken),
            command.RequestId,
            customer.Id,
            command.AmountCents,
            command.Method,
            command.Reference,
            shift.Value?.Id,
            balance);
        _payments.Add(payment);

        var paid = new List<Guid>();
        foreach (var allocation in allocations)
        {
            var receivable = pending.Single(r => r.Id == allocation.ReceivableId);
            receivable.ApplyPayment(payment.Id, allocation.AmountCents);
            if (receivable.Status == ReceivableStatus.Paid)
            {
                paid.Add(receivable.Id);
            }
        }

        _audit.Add(
            AuditActions.CustomerPaymentRegistered,
            AuditActions.CustomerPaymentEntity,
            payment.Id,
            $"Abono {payment.Folio}. Cliente: {customer.Name}. Monto {TicketBuilder.FormatMoney(payment.AmountCents)}. Forma de pago: {payment.Method.ToCode()}. "
                + $"Saldo anterior {TicketBuilder.FormatMoney(payment.BalanceBeforeCents)}. Saldo nuevo {TicketBuilder.FormatMoney(payment.BalanceAfterCents)}");

        var outcome = await _payments.SaveChangesAsync(cancellationToken);
        if (outcome.Status == SaveStatus.Duplicate && outcome.DuplicateField == ReceivableFields.RequestId)
        {
            // Otro intento con la misma clave ya quedó registrado: se devuelve ese abono.
            return await _payments.FindByRequestAsync(command.RequestId, cancellationToken) is { } registered
                ? (Result.Success(await ReceiptOfAsync(registered, cancellationToken)), false)
                : (Result.Failure<PaymentReceipt>(new Conflict()), false);
        }

        if (outcome.Status != SaveStatus.Saved)
        {
            return (Result.Failure<PaymentReceipt>(new Conflict()), outcome.Status == SaveStatus.Duplicate);
        }

        await transaction.CommitAsync(cancellationToken);
        LogRegistered(payment.Id, payment.Folio, customer.Id, _currentUser.UserId, payment.AmountCents, payment.Method, balance, payment.BalanceAfterCents);
        return (Result.Success(new PaymentReceipt(payment.Id, payment.Folio, payment.BalanceBeforeCents, payment.BalanceAfterCents, paid)), false);
    }

    /// <summary>Recibo de un abono ya registrado: las cuentas que hoy siguen pagadas entre las que tocó.</summary>
    private async Task<PaymentReceipt> ReceiptOfAsync(CustomerPayment payment, CancellationToken cancellationToken)
    {
        var entries = await _receivables.GetEntriesByPaymentAsync(payment.Id, cancellationToken);
        var receivables = await _receivables.GetManyAsync([.. entries.Select(e => e.ReceivableId).Distinct()], cancellationToken);
        return new PaymentReceipt(
            payment.Id,
            payment.Folio,
            payment.BalanceBeforeCents,
            payment.BalanceAfterCents,
            [.. receivables.Where(r => r.Status == ReceivableStatus.Paid).Select(r => r.Id)]);
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Abono registrado. PaymentId={PaymentId} Folio={Folio} CustomerId={CustomerId} UserId={UserId} AmountCents={AmountCents} Metodo={Method} SaldoAnteriorCents={BalanceBefore} SaldoNuevoCents={BalanceAfter}")]
    private partial void LogRegistered(Guid paymentId, string folio, Guid customerId, Guid userId, long amountCents, PaymentMethod method, long balanceBefore, long balanceAfter);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Abono rechazado: el monto excede el saldo. CustomerId={CustomerId} AmountCents={AmountCents} SaldoCents={BalanceCents}")]
    private partial void LogExceedsBalance(Guid customerId, long amountCents, long balanceCents);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Abono rechazado por el turno. CustomerId={CustomerId} Motivo={Reason}")]
    private partial void LogShiftRejected(Guid customerId, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Folio de abono duplicado; se reintenta. RequestId={RequestId} Intento={Attempt}")]
    private partial void LogRetry(Guid requestId, int attempt);
}
