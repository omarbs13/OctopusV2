using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Products;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Suppliers.SetSupplierActive;

/// <summary>Activa o desactiva un proveedor (<c>ManageSuppliers</c>); siempre se permite (FR-006).</summary>
public sealed partial class SetSupplierActiveHandler
{
    private readonly IAccessControl _access;
    private readonly ISupplierRepository _suppliers;
    private readonly IAuditLog _audit;
    private readonly IWriteTransactions _transactions;
    private readonly ILogger<SetSupplierActiveHandler> _logger;

    public SetSupplierActiveHandler(
        IAccessControl access,
        ISupplierRepository suppliers,
        IAuditLog audit,
        IWriteTransactions transactions,
        ILogger<SetSupplierActiveHandler> logger)
    {
        _access = access;
        _suppliers = suppliers;
        _audit = audit;
        _transactions = transactions;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(SetSupplierActiveCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var access = await _access.CheckAsync(Permission.ManageSuppliers, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure(access.Error!);
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

        if (supplier.IsActive == command.Active)
        {
            return Result.Success();
        }

        var before = SupplierAuditFields.Snapshot(supplier);
        if (command.Active)
        {
            supplier.Activate();
        }
        else
        {
            supplier.Deactivate();
        }

        _audit.Add(new AuditRecord(
            command.Active ? AuditActions.SupplierActivated : AuditActions.SupplierDeactivated,
            AuditActions.SupplierEntity,
            supplier.Id,
            EntityName: supplier.Name,
            Changes: AuditChanges.Compare(before, SupplierAuditFields.Snapshot(supplier))));

        var outcome = await _suppliers.SaveChangesAsync(cancellationToken);
        if (outcome.Status != SaveStatus.Saved)
        {
            return Result.Failure(new Conflict());
        }

        await transaction.CommitAsync(cancellationToken);
        LogChanged(supplier.Id, command.Active);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Proveedor activado o desactivado. SupplierId={SupplierId} Activo={Active}")]
    private partial void LogChanged(Guid supplierId, bool active);
}
