using FluentValidation;
using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Discounts;
using Pos.Application.Inventory;
using Pos.Application.Products;
using Pos.Application.Suppliers;
using Pos.Application.Users.Access;
using Pos.Domain.Common;
using Pos.Domain.Inventory;
using Pos.Domain.Products;
using Pos.Domain.Purchases;
using Pos.Domain.Users;

namespace Pos.Application.Purchases.RegisterPurchase;

/// <summary>
/// Registra una compra y su entrada de mercancía en una sola transacción, en el orden de
/// contracts/application-ports.md: permiso → forma → fecha → <c>BEGIN IMMEDIATE</c> → proveedor activo →
/// productos → importes → factura duplicada → compra, movimientos <c>PURCHASE</c> y enlaces → bitácora → un solo
/// guardado. Los errores de campo de todos los pasos se juntan en un solo <see cref="ValidationFailed"/>
/// (escenario 6). Registrar una compra no modifica el producto (FR-015, SC-002).
/// </summary>
public sealed partial class RegisterPurchaseHandler
{
    private readonly IAccessControl _access;
    private readonly ISupplierRepository _suppliers;
    private readonly IProductRepository _products;
    private readonly IInventoryRepository _inventory;
    private readonly IPurchaseRepository _purchases;
    private readonly IAuditLog _audit;
    private readonly IWriteTransactions _transactions;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<RegisterPurchaseCommand> _validator;
    private readonly ILogger<RegisterPurchaseHandler> _logger;

    public RegisterPurchaseHandler(
        IAccessControl access,
        ISupplierRepository suppliers,
        IProductRepository products,
        IInventoryRepository inventory,
        IPurchaseRepository purchases,
        IAuditLog audit,
        IWriteTransactions transactions,
        IClock clock,
        ICurrentUser currentUser,
        IValidator<RegisterPurchaseCommand> validator,
        ILogger<RegisterPurchaseHandler> logger)
    {
        _access = access;
        _suppliers = suppliers;
        _products = products;
        _inventory = inventory;
        _purchases = purchases;
        _audit = audit;
        _transactions = transactions;
        _clock = clock;
        _currentUser = currentUser;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<PurchaseRegisteredDto>> HandleAsync(RegisterPurchaseCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // 1. Permiso (y licencia) y forma.
        var access = await _access.CheckAsync(Permission.RegisterPurchases, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<PurchaseRegisteredDto>(access.Error!);
        }

        var errors = new FieldErrors();
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        errors.AddRange(ProductRules.ToError(validation).Errors);
        var lines = command.Lines ?? [];

        // 2. Fecha de factura no futura (día local).
        var today = DiscountDates.LocalToday(_clock);
        if (command.InvoiceDate > today)
        {
            errors.Add(PurchaseFields.InvoiceDate, PurchaseMessages.InvoiceDateFuture);
        }

        // 3. Escritores serializados con las ventas y las demás compras.
        await using var transaction = await _transactions.BeginAsync(cancellationToken);

        // 4. Proveedor existente y activo.
        var supplier = command.SupplierId == Guid.Empty ? null : await _suppliers.GetAsync(command.SupplierId, cancellationToken);
        if (command.SupplierId != Guid.Empty)
        {
            if (supplier is null)
            {
                errors.Add(PurchaseFields.SupplierId, PurchaseMessages.SupplierNotFound);
            }
            else if (!supplier.IsActive)
            {
                errors.Add(PurchaseFields.SupplierId, PurchaseMessages.SupplierInactive);
            }
        }

        // 5. Productos existentes, no borrados, activos y que controlan inventario.
        var productIds = lines.Select(l => l.ProductId).Where(id => id != Guid.Empty).Distinct().ToList();
        var products = (await _products.GetManyAsync(productIds, includeDeleted: true, cancellationToken)).ToDictionary(p => p.Id);
        var stocks = await _inventory.GetStocksAsync(productIds, cancellationToken);
        var calculationLines = new List<PurchaseCalculationLine>(lines.Count);
        for (var i = 0; i < lines.Count; i++)
        {
            var product = products.GetValueOrDefault(lines[i].ProductId);
            var unit = product is null ? null : UnitOfMeasure.Find(product.UnitCode);
            if (lines[i].ProductId != Guid.Empty)
            {
                if (product is null || product.IsDeleted)
                {
                    errors.Add(PurchaseFields.LineProduct(i), PurchaseMessages.ProductNotFound);
                }
                else if (!product.IsActive)
                {
                    errors.Add(PurchaseFields.LineProduct(i), PurchaseMessages.ProductInactive);
                }
                else if (!product.TracksInventory)
                {
                    errors.Add(PurchaseFields.LineProduct(i), PurchaseMessages.ProductNotTracked);
                }
            }

            // Los decimales de la unidad del producto (escenarios 7 y 9); sin producto, los de la captura.
            calculationLines.Add(new PurchaseCalculationLine(unit?.DecimalPlaces ?? 3, unit?.Name ?? string.Empty, lines[i].QuantityText, lines[i].UnitCostText));
        }

        // 6. Importes con el mismo núcleo que la captura en vivo (research §4).
        var calculation = PurchaseCalculator.Calculate(calculationLines, command.TaxText);
        errors.AddRange(calculation.AllErrors);
        for (var i = 0; i < lines.Count; i++)
        {
            if (calculation.Lines[i].QuantityThousandths is { } quantity
                && stocks.TryGetValue(lines[i].ProductId, out var current)
                && ProductStock.WouldExceedMaximum(current.OnHand, Quantity.FromThousandths(quantity)))
            {
                errors.Add(PurchaseFields.LineQuantity(i), PurchaseMessages.StockExceeded);
            }
        }

        if (errors.Count > 0 || supplier is null || command.InvoiceDate is not { } invoiceDate)
        {
            LogRejected(command.SupplierId, errors.Count);
            return Result.Failure<PurchaseRegisteredDto>(errors.ToError());
        }

        // 7. Factura vigente duplicada del mismo proveedor (FR-011).
        var invoiceKey = Purchase.NormalizeInvoice(command.InvoiceNumber);
        if (await _purchases.FindActiveByInvoiceAsync(supplier.Id, invoiceKey, cancellationToken) is { } existing)
        {
            LogDuplicate(supplier.Id, invoiceKey, existing.Id);
            return Result.Failure<PurchaseRegisteredDto>(new DuplicateInvoice(existing.Id, existing.InvoiceDate, existing.CreatedAt));
        }

        // 8. Compra con los datos congelados, un movimiento PURCHASE por línea y su enlace.
        var drafts = lines.Select((line, i) =>
        {
            var product = products[line.ProductId];
            return new PurchaseLineDraft(
                product.Id,
                product.Name,
                product.Sku,
                product.UnitCode,
                calculation.Lines[i].QuantityThousandths!.Value,
                calculation.Lines[i].UnitCostCents!.Value);
        }).ToList();
        var purchase = Purchase.Register(supplier.Id, supplier.Name, command.InvoiceNumber, invoiceDate, today, drafts, calculation.TaxCents!.Value);
        _purchases.Add(purchase);

        foreach (var line in purchase.Lines)
        {
            var product = products[line.ProductId];
            if (!stocks.TryGetValue(line.ProductId, out var stock))
            {
                stock = ProductStock.Start(line.ProductId);
                _inventory.AddStock(stock);
            }

            var movement = stock.RecordPurchase(
                Quantity.FromThousandths(line.QuantityThousandths),
                UnitOfMeasure.Find(product.UnitCode)!,
                product.IsActive,
                product.TracksInventory,
                purchase.InvoiceNumber);
            _inventory.AddMovement(movement);
            purchase.LinkMovement(line.ProductId, movement.Id);
        }

        // 9. Bitácora.
        _audit.Add(new AuditRecord(
            AuditActions.PurchaseRegistered,
            AuditActions.PurchaseEntity,
            purchase.Id,
            EntityName: PurchaseAudit.EntityName(purchase),
            Changes: AuditChanges.Created(PurchaseAudit.Snapshot(purchase))));

        // 10. Un solo guardado.
        var outcome = await _purchases.SaveChangesAsync(cancellationToken);
        if (outcome.Status != SaveStatus.Saved)
        {
            if (outcome.Status == SaveStatus.Duplicate
                && await _purchases.FindActiveByInvoiceAsync(supplier.Id, invoiceKey, cancellationToken) is { } concurrent)
            {
                LogDuplicate(supplier.Id, invoiceKey, concurrent.Id);
                return Result.Failure<PurchaseRegisteredDto>(new DuplicateInvoice(concurrent.Id, concurrent.InvoiceDate, concurrent.CreatedAt));
            }

            return Result.Failure<PurchaseRegisteredDto>(new Conflict());
        }

        // 11. Confirmar y registrar para soporte.
        await transaction.CommitAsync(cancellationToken);
        LogRegistered(purchase.Id, supplier.Id, purchase.InvoiceNumber, purchase.LineCount, purchase.TotalCents, _currentUser.UserId);
        return Result.Success(new PurchaseRegisteredDto(purchase.Id, purchase.SubtotalCents, purchase.TaxCents, purchase.TotalCents));
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Compra registrada. PurchaseId={PurchaseId} SupplierId={SupplierId} Factura={Invoice} Lineas={LineCount} TotalCents={TotalCents} UserId={UserId}")]
    private partial void LogRegistered(Guid purchaseId, Guid supplierId, string invoice, int lineCount, long totalCents, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Compra rechazada por datos inválidos. SupplierId={SupplierId} Errores={ErrorCount}")]
    private partial void LogRejected(Guid supplierId, int errorCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Compra rechazada: factura duplicada. SupplierId={SupplierId} Factura={InvoiceKey} CompraExistente={ExistingId}")]
    private partial void LogDuplicate(Guid supplierId, string invoiceKey, Guid existingId);

    /// <summary>Errores de campo sin repetir campo: se conserva el primero (el más básico).</summary>
    private sealed class FieldErrors
    {
        private readonly List<FieldError> _errors = [];

        public int Count => _errors.Count;

        public void Add(string field, string message) => Add(new FieldError(field, message));

        public void AddRange(IEnumerable<FieldError> errors)
        {
            foreach (var error in errors)
            {
                Add(error);
            }
        }

        public ValidationFailed ToError() => new([.. _errors]);

        private void Add(FieldError error)
        {
            if (!_errors.Any(e => e.Field == error.Field))
            {
                _errors.Add(error);
            }
        }
    }
}
