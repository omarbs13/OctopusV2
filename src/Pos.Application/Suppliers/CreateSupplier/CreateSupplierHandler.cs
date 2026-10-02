using FluentValidation;
using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Products;
using Pos.Application.Users.Access;
using Pos.Domain.Suppliers;
using Pos.Domain.Users;

namespace Pos.Application.Suppliers.CreateSupplier;

/// <summary>
/// Alta de proveedor: licencia y permiso <c>ManageSuppliers</c> → validación → RUC libre → alta y bitácora en
/// una transacción. El RUC repetido (también por el índice) es <see cref="SupplierTaxIdInUse"/>.
/// </summary>
public sealed partial class CreateSupplierHandler
{
    private readonly IAccessControl _access;
    private readonly ISupplierRepository _suppliers;
    private readonly IAuditLog _audit;
    private readonly IWriteTransactions _transactions;
    private readonly IValidator<CreateSupplierCommand> _validator;
    private readonly ILogger<CreateSupplierHandler> _logger;

    public CreateSupplierHandler(
        IAccessControl access,
        ISupplierRepository suppliers,
        IAuditLog audit,
        IWriteTransactions transactions,
        IValidator<CreateSupplierCommand> validator,
        ILogger<CreateSupplierHandler> logger)
    {
        _access = access;
        _suppliers = suppliers;
        _audit = audit;
        _transactions = transactions;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<Guid>> HandleAsync(CreateSupplierCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var access = await _access.CheckAsync(Permission.ManageSuppliers, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<Guid>(access.Error!);
        }

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<Guid>(ProductRules.ToError(validation));
        }

        await using var transaction = await _transactions.BeginAsync(cancellationToken);

        var inUse = await SupplierSaving.TaxIdInUseAsync(_suppliers, command.TaxId, null, cancellationToken);
        if (inUse is not null)
        {
            return Result.Failure<Guid>(inUse);
        }

        var supplier = Supplier.Create(
            command.Name,
            command.TaxId,
            command.Phone,
            command.Email,
            command.Address,
            command.PaymentTerms,
            SupplierRules.ParseCreditDays(command.CreditDaysText));
        _suppliers.Add(supplier);
        _audit.Add(new AuditRecord(
            AuditActions.SupplierCreated,
            AuditActions.SupplierEntity,
            supplier.Id,
            EntityName: supplier.Name,
            Changes: AuditChanges.Created(SupplierAuditFields.Snapshot(supplier))));

        var outcome = await _suppliers.SaveChangesAsync(cancellationToken);
        if (outcome.Status != SaveStatus.Saved)
        {
            return Result.Failure<Guid>(await SupplierSaving.ToErrorAsync(_suppliers, outcome, command.TaxId, null, cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
        LogCreated(supplier.Id, supplier.PaymentTerms);
        return Result.Success(supplier.Id);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Proveedor creado. SupplierId={SupplierId} Condiciones={Terms}")]
    private partial void LogCreated(Guid supplierId, PaymentTerms terms);
}
