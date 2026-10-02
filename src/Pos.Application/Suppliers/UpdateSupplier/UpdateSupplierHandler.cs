using FluentValidation;
using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Products;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Suppliers.UpdateSupplier;

/// <summary>
/// Edición de proveedor con concurrencia optimista. La bitácora registra solo los campos que cambiaron y
/// nada si no cambió ninguno (research §14). Las compras guardan su propio nombre (FR-005, FR-016).
/// </summary>
public sealed partial class UpdateSupplierHandler
{
    private readonly IAccessControl _access;
    private readonly ISupplierRepository _suppliers;
    private readonly IAuditLog _audit;
    private readonly IWriteTransactions _transactions;
    private readonly IValidator<UpdateSupplierCommand> _validator;
    private readonly ILogger<UpdateSupplierHandler> _logger;

    public UpdateSupplierHandler(
        IAccessControl access,
        ISupplierRepository suppliers,
        IAuditLog audit,
        IWriteTransactions transactions,
        IValidator<UpdateSupplierCommand> validator,
        ILogger<UpdateSupplierHandler> logger)
    {
        _access = access;
        _suppliers = suppliers;
        _audit = audit;
        _transactions = transactions;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(UpdateSupplierCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var access = await _access.CheckAsync(Permission.ManageSuppliers, cancellationToken);
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

        var supplier = await _suppliers.GetAsync(command.Id, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure(new NotFound());
        }

        if (supplier.Version != command.ExpectedVersion)
        {
            return Result.Failure(new Conflict());
        }

        var inUse = await SupplierSaving.TaxIdInUseAsync(_suppliers, command.TaxId, supplier.Id, cancellationToken);
        if (inUse is not null)
        {
            return Result.Failure(inUse);
        }

        var before = SupplierAuditFields.Snapshot(supplier);
        supplier.Update(
            command.Name,
            command.TaxId,
            command.Phone,
            command.Email,
            command.Address,
            command.PaymentTerms,
            SupplierRules.ParseCreditDays(command.CreditDaysText));
        var changes = AuditChanges.Compare(before, SupplierAuditFields.Snapshot(supplier));
        if (!AuditChanges.HasChanges(changes))
        {
            return Result.Success();
        }

        _audit.Add(new AuditRecord(
            AuditActions.SupplierUpdated,
            AuditActions.SupplierEntity,
            supplier.Id,
            EntityName: supplier.Name,
            Changes: changes));

        var outcome = await _suppliers.SaveChangesAsync(cancellationToken);
        if (outcome.Status != SaveStatus.Saved)
        {
            return Result.Failure(await SupplierSaving.ToErrorAsync(_suppliers, outcome, command.TaxId, command.Id, cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
        LogUpdated(supplier.Id, changes.Count);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Proveedor modificado. SupplierId={SupplierId} Campos={FieldCount}")]
    private partial void LogUpdated(Guid supplierId, int fieldCount);
}
