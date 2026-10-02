using FluentValidation;
using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Printing.Ticket;
using Pos.Application.Products;
using Pos.Application.Users.Access;
using Pos.Domain.Customers;
using Pos.Domain.Users;

namespace Pos.Application.Customers.UpdateCustomer;

/// <summary>
/// Edición de cliente con concurrencia optimista. Cambiar límite o modalidad sin
/// <c>ManageCustomerCredit</c> es <see cref="Forbidden"/>; si cambió el crédito se audita aparte con
/// los valores anterior y nuevo.
/// </summary>
public sealed partial class UpdateCustomerHandler
{
    private readonly IAccessControl _access;
    private readonly ICustomerRepository _customers;
    private readonly IAuditLog _audit;
    private readonly IWriteTransactions _transactions;
    private readonly IValidator<UpdateCustomerCommand> _validator;
    private readonly ILogger<UpdateCustomerHandler> _logger;

    public UpdateCustomerHandler(
        IAccessControl access,
        ICustomerRepository customers,
        IAuditLog audit,
        IWriteTransactions transactions,
        IValidator<UpdateCustomerCommand> validator,
        ILogger<UpdateCustomerHandler> logger)
    {
        _access = access;
        _customers = customers;
        _audit = audit;
        _transactions = transactions;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(UpdateCustomerCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var access = await _access.CheckAsync(Permission.ManageCustomers, cancellationToken);
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

        var customer = await _customers.GetAsync(command.Id, cancellationToken);
        if (customer is null)
        {
            return Result.Failure(new NotFound());
        }

        if (customer.Version != command.ExpectedVersion)
        {
            return Result.Failure(new Conflict());
        }

        var previousLimit = customer.CreditLimitCents;
        var previousMode = customer.CreditMode;
        var limit = command.CreditLimitCents ?? previousLimit;
        var mode = command.CreditMode ?? previousMode;
        var creditChanged = limit != previousLimit || mode != previousMode;
        if (creditChanged)
        {
            var credit = await _access.CheckAsync(Permission.ManageCustomerCredit, cancellationToken);
            if (!credit.Allowed)
            {
                return Result.Failure(credit.Error!);
            }
        }

        var before = CustomerAuditFields.Snapshot(customer);
        customer.Update(command.Name, command.Phone, command.Email, command.TaxId);
        if (creditChanged)
        {
            customer.ChangeCredit(limit, mode);
        }

        // Los datos van en CUSTOMER_UPDATED y el crédito en CUSTOMER_CREDIT_CHANGED (018, FR-002).
        var changes = AuditChanges.Compare(before, CustomerAuditFields.Snapshot(customer));
        var dataChanges = changes.Where(c => !IsCreditField(c.Field)).ToList();
        if (AuditChanges.HasChanges(dataChanges))
        {
            _audit.Add(new AuditRecord(
                AuditActions.CustomerUpdated,
                AuditActions.CustomerEntity,
                customer.Id,
                EntityName: customer.Name,
                Details: CustomerRules.Describe(customer),
                Changes: dataChanges));
        }

        if (creditChanged)
        {
            _audit.Add(new AuditRecord(
                AuditActions.CustomerCreditChanged,
                AuditActions.CustomerEntity,
                customer.Id,
                EntityName: customer.Name,
                Details: $"Cliente: {customer.Name}. Antes: {previousMode.ToCode()}, {TicketBuilder.FormatMoney(previousLimit)}. Ahora: {mode.ToCode()}, {TicketBuilder.FormatMoney(limit)}",
                Changes: [.. changes.Where(c => IsCreditField(c.Field))]));
        }

        var outcome = await _customers.SaveChangesAsync(cancellationToken);
        if (outcome.Status != SaveStatus.Saved)
        {
            return Result.Failure(outcome.Status == SaveStatus.Duplicate ? new Duplicate(CustomerFields.TaxId) : new Conflict());
        }

        await transaction.CommitAsync(cancellationToken);
        LogUpdated(customer.Id, creditChanged);
        return Result.Success();
    }

    private static bool IsCreditField(string field) =>
        field is CustomerAuditFields.CreditMode or CustomerAuditFields.CreditLimit;

    [LoggerMessage(Level = LogLevel.Information, Message = "Cliente modificado. CustomerId={CustomerId} CreditoModificado={CreditChanged}")]
    private partial void LogUpdated(Guid customerId, bool creditChanged);
}
