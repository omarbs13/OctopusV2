using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Products;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Customers.SetCustomerActive;

/// <summary>
/// Activa o desactiva un cliente (<c>ManageCustomerCredit</c>). Desactivar exige saldo 0, leído dentro de
/// la transacción para que una venta a crédito simultánea no se cuele (FR-004). Reactivar no tiene condición.
/// </summary>
public sealed partial class SetCustomerActiveHandler
{
    private readonly IAccessControl _access;
    private readonly ICustomerRepository _customers;
    private readonly IAuditLog _audit;
    private readonly IWriteTransactions _transactions;
    private readonly ILogger<SetCustomerActiveHandler> _logger;

    public SetCustomerActiveHandler(
        IAccessControl access,
        ICustomerRepository customers,
        IAuditLog audit,
        IWriteTransactions transactions,
        ILogger<SetCustomerActiveHandler> logger)
    {
        _access = access;
        _customers = customers;
        _audit = audit;
        _transactions = transactions;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(SetCustomerActiveCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var access = await _access.CheckAsync(Permission.ManageCustomerCredit, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure(access.Error!);
        }

        await using var transaction = await _transactions.BeginAsync(cancellationToken);

        var customer = await _customers.GetAsync(command.Id, cancellationToken);
        if (customer is null)
        {
            return Result.Failure(new NotFound());
        }

        if (customer.Version != command.ExpectedVersion)
        {
            return Result.Failure(new Conflict());
        }

        if (command.Active)
        {
            customer.Activate();
            _audit.Add(AuditActions.CustomerActivated, AuditActions.CustomerEntity, customer.Id, $"Cliente: {customer.Name}");
        }
        else
        {
            var balance = await _customers.GetBalanceAsync(customer.Id, cancellationToken);
            if (balance > 0)
            {
                LogHasBalance(customer.Id, balance);
                return Result.Failure(new CustomerHasBalance(balance));
            }

            customer.Deactivate(balance);
            _audit.Add(AuditActions.CustomerDeactivated, AuditActions.CustomerEntity, customer.Id, $"Cliente: {customer.Name}");
        }

        var outcome = await _customers.SaveChangesAsync(cancellationToken);
        if (outcome.Status != SaveStatus.Saved)
        {
            return Result.Failure(new Conflict());
        }

        await transaction.CommitAsync(cancellationToken);
        LogChanged(customer.Id, command.Active);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Cliente activado o desactivado. CustomerId={CustomerId} Activo={Active}")]
    private partial void LogChanged(Guid customerId, bool active);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Desactivación de cliente rechazada por saldo pendiente. CustomerId={CustomerId} BalanceCents={BalanceCents}")]
    private partial void LogHasBalance(Guid customerId, long balanceCents);
}
