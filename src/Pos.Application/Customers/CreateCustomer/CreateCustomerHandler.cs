using FluentValidation;
using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Products;
using Pos.Application.Users.Access;
using Pos.Domain.Customers;
using Pos.Domain.Users;

namespace Pos.Application.Customers.CreateCustomer;

/// <summary>
/// Alta de cliente: licencia y permiso <c>ManageCustomers</c> → validación → alta y bitácora en una
/// transacción. El RUC repetido se traduce a <see cref="Duplicate"/> de <see cref="CustomerFields.TaxId"/>.
/// </summary>
public sealed partial class CreateCustomerHandler
{
    private readonly IAccessControl _access;
    private readonly ICustomerRepository _customers;
    private readonly IAuditLog _audit;
    private readonly IWriteTransactions _transactions;
    private readonly IValidator<CreateCustomerCommand> _validator;
    private readonly ILogger<CreateCustomerHandler> _logger;

    public CreateCustomerHandler(
        IAccessControl access,
        ICustomerRepository customers,
        IAuditLog audit,
        IWriteTransactions transactions,
        IValidator<CreateCustomerCommand> validator,
        ILogger<CreateCustomerHandler> logger)
    {
        _access = access;
        _customers = customers;
        _audit = audit;
        _transactions = transactions;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<Guid>> HandleAsync(CreateCustomerCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var access = await _access.CheckAsync(Permission.ManageCustomers, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<Guid>(access.Error!);
        }

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<Guid>(ProductRules.ToError(validation));
        }

        // FR-020: sin ManageCustomerCredit se ignoran límite y modalidad, sin rechazar el alta.
        var canAssignCredit = await _access.HasAsync(Permission.ManageCustomerCredit, cancellationToken);
        var limit = canAssignCredit ? command.CreditLimitCents ?? 0 : 0;
        var mode = canAssignCredit ? command.CreditMode ?? CreditMode.CashOnly : CreditMode.CashOnly;

        await using var transaction = await _transactions.BeginAsync(cancellationToken);

        var customer = Customer.Create(command.Name, command.Phone, command.Email, command.TaxId, limit, mode);
        _customers.Add(customer);
        _audit.Add(new AuditRecord(
            AuditActions.CustomerCreated,
            AuditActions.CustomerEntity,
            customer.Id,
            EntityName: customer.Name,
            Details: CustomerRules.Describe(customer),
            Changes: AuditChanges.Created(CustomerAuditFields.Snapshot(customer))));

        var outcome = await _customers.SaveChangesAsync(cancellationToken);
        if (outcome.Status != SaveStatus.Saved)
        {
            return Result.Failure<Guid>(outcome.Status == SaveStatus.Duplicate ? new Duplicate(CustomerFields.TaxId) : new Conflict());
        }

        await transaction.CommitAsync(cancellationToken);
        LogCreated(customer.Id, customer.CreditMode, customer.CreditLimitCents);
        return Result.Success(customer.Id);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Cliente creado. CustomerId={CustomerId} Modalidad={Mode} LimitCents={LimitCents}")]
    private partial void LogCreated(Guid customerId, CreditMode mode, long limitCents);
}
