using FluentValidation;
using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Inventory;
using Pos.Application.Products;
using Pos.Application.Users.Access;
using Pos.Domain.Common;
using Pos.Domain.Purchases;
using Pos.Domain.Users;

namespace Pos.Application.Purchases.VoidPurchase;

/// <summary>
/// Anula una compra completa en una transacción, en el orden de research §7: licencia → permiso (no autorizable) →
/// motivo → compra existente → vigente → versión → cada línea revertible (producto activo, que controla inventario
/// y con existencia suficiente). Si alguna no lo es, <see cref="PurchaseVoidBlocked"/> con todas y nada cambia; si
/// todas lo son, un <c>PURCH_VOID</c> por línea, la compra anulada y la bitácora, en un solo guardado.
/// </summary>
public sealed partial class VoidPurchaseHandler
{
    private readonly IAccessControl _access;
    private readonly IPurchaseRepository _purchases;
    private readonly IProductRepository _products;
    private readonly IInventoryRepository _inventory;
    private readonly IAuditLog _audit;
    private readonly IWriteTransactions _transactions;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<VoidPurchaseCommand> _validator;
    private readonly ILogger<VoidPurchaseHandler> _logger;

    public VoidPurchaseHandler(
        IAccessControl access,
        IPurchaseRepository purchases,
        IProductRepository products,
        IInventoryRepository inventory,
        IAuditLog audit,
        IWriteTransactions transactions,
        IClock clock,
        ICurrentUser currentUser,
        IValidator<VoidPurchaseCommand> validator,
        ILogger<VoidPurchaseHandler> logger)
    {
        _access = access;
        _purchases = purchases;
        _products = products;
        _inventory = inventory;
        _audit = audit;
        _transactions = transactions;
        _clock = clock;
        _currentUser = currentUser;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(VoidPurchaseCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var access = await _access.CheckAsync(Permission.VoidPurchases, cancellationToken);
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

        var purchase = await _purchases.GetAsync(command.PurchaseId, cancellationToken);
        if (purchase is null)
        {
            return Result.Failure(new NotFound());
        }

        if (purchase.Status != PurchaseStatus.Active)
        {
            return Result.Failure(new InvalidState(PurchaseMessages.AlreadyVoided));
        }

        if (purchase.Version != command.ExpectedVersion)
        {
            return Result.Failure(new Conflict());
        }

        // La existencia se lee dentro de la transacción: ya incluye cualquier venta anterior (research §6).
        var productIds = purchase.Lines.Select(l => l.ProductId).ToList();
        var products = (await _products.GetManyAsync(productIds, includeDeleted: true, cancellationToken)).ToDictionary(p => p.Id);
        var stocks = await _inventory.GetStocksAsync(productIds, cancellationToken);
        var blockers = new List<PurchaseVoidBlocker>();
        foreach (var line in purchase.Lines)
        {
            var product = products.GetValueOrDefault(line.ProductId);
            var onHand = stocks.TryGetValue(line.ProductId, out var stock) ? stock.OnHandThousandths : 0;
            VoidBlockReason? reason = product is null || product.IsDeleted || !product.IsActive ? VoidBlockReason.ProductInactive
                : !product.TracksInventory ? VoidBlockReason.NotTracked
                : onHand < line.QuantityThousandths ? VoidBlockReason.InsufficientStock
                : null;
            if (reason is { } blocked)
            {
                blockers.Add(new PurchaseVoidBlocker(line.ProductId, line.ProductName, blocked, onHand, line.QuantityThousandths));
            }
        }

        if (blockers.Count > 0)
        {
            LogBlocked(purchase.Id, purchase.InvoiceNumber, string.Join(", ", blockers.Select(b => $"{b.ProductId}:{b.Reason}")));
            return Result.Failure(new PurchaseVoidBlocked(blockers));
        }

        foreach (var line in purchase.Lines)
        {
            var product = products[line.ProductId];
            var movement = stocks[line.ProductId].RecordPurchaseVoid(
                Quantity.FromThousandths(line.QuantityThousandths),
                product.IsActive,
                product.TracksInventory,
                purchase.InvoiceNumber);
            _inventory.AddMovement(movement);
            purchase.LinkVoidMovement(line.ProductId, movement.Id);
        }

        var reasonText = command.Reason.Trim();
        purchase.Void(reasonText, _currentUser.UserId, _clock.UtcNow);
        _audit.Add(new AuditRecord(
            AuditActions.PurchaseVoided,
            AuditActions.PurchaseEntity,
            purchase.Id,
            EntityName: PurchaseAudit.EntityName(purchase),
            Reason: reasonText,
            Changes:
            [
                new(PurchaseAudit.State, PurchaseStatus.Active.Describe(), PurchaseStatus.Voided.Describe()),
                .. AuditChanges.Removed(PurchaseAudit.Amounts(purchase)),
            ]));

        var outcome = await _purchases.SaveChangesAsync(cancellationToken);
        if (outcome.Status != SaveStatus.Saved)
        {
            return Result.Failure(new Conflict());
        }

        await transaction.CommitAsync(cancellationToken);
        LogVoided(purchase.Id, purchase.SupplierId, purchase.InvoiceNumber, purchase.LineCount, purchase.TotalCents, _currentUser.UserId);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Compra anulada. PurchaseId={PurchaseId} SupplierId={SupplierId} Factura={Invoice} Lineas={LineCount} TotalCents={TotalCents} UserId={UserId}")]
    private partial void LogVoided(Guid purchaseId, Guid supplierId, string invoice, int lineCount, long totalCents, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Anulación de compra rechazada. PurchaseId={PurchaseId} Factura={Invoice} Productos={Blockers}")]
    private partial void LogBlocked(Guid purchaseId, string invoice, string blockers);
}
