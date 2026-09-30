using FluentValidation;
using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Inventory;
using Pos.Application.Products;
using Pos.Domain.Common;
using Pos.Domain.Inventory;
using Pos.Domain.Products;
using Pos.Domain.Sales;

namespace Pos.Application.Sales.ConfirmSale;

/// <summary>
/// Registra la venta en una sola transacción de escritura (research §4): folio consecutivo, venta,
/// líneas con copia de datos, pagos, movimientos <c>SALE</c> y borrado del borrador. Una falla no
/// consume folio ni deja nada a medias, y el <c>DraftId</c> vuelve idempotente el reintento.
/// </summary>
public sealed partial class ConfirmSaleHandler
{
    private readonly IProductRepository _products;
    private readonly IInventoryRepository _inventory;
    private readonly ISaleRepository _sales;
    private readonly ISaleDraftStore _drafts;
    private readonly IWriteTransactions _transactions;
    private readonly IValidator<ConfirmSaleCommand> _validator;
    private readonly ILogger<ConfirmSaleHandler> _logger;

    public ConfirmSaleHandler(
        IProductRepository products,
        IInventoryRepository inventory,
        ISaleRepository sales,
        ISaleDraftStore drafts,
        IWriteTransactions transactions,
        IValidator<ConfirmSaleCommand> validator,
        ILogger<ConfirmSaleHandler> logger)
    {
        _products = products;
        _inventory = inventory;
        _sales = sales;
        _drafts = drafts;
        _transactions = transactions;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<ConfirmedSale>> HandleAsync(ConfirmSaleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<ConfirmedSale>(ProductRules.ToError(validation));
        }

        await using var transaction = await _transactions.BeginAsync(cancellationToken);

        if (await _sales.FindByDraftAsync(command.DraftId, cancellationToken) is { } existing)
        {
            return Result.Failure<ConfirmedSale>(new AlreadyRegistered(existing.SaleId, existing.Folio));
        }

        var ids = command.Lines.Select(l => l.ProductId).ToList();
        var products = (await _products.GetManyAsync(ids, includeDeleted: true, cancellationToken)).ToDictionary(p => p.Id);
        var tracked = products.Values.Where(p => p.TracksInventory).Select(p => p.Id).ToList();
        var stocks = tracked.Count == 0
            ? []
            : (await _inventory.GetStocksAsync(tracked, cancellationToken)).ToDictionary(s => s.Key, s => s.Value);

        var reviews = command.Lines
            .Select(l => SaleReviewer.Review(l.ProductId, l.QuantityThousandths, products.GetValueOrDefault(l.ProductId), stocks))
            .ToList();
        var changed = reviews.Zip(command.Lines).Any(x =>
            x.First.NotSellableReason is not null || x.First.CurrentPriceCents != x.Second.ExpectedUnitPriceCents);
        if (changed)
        {
            LogChanged(command.DraftId, command.Lines.Count);
            return Result.Failure<ConfirmedSale>(new SaleChanged(reviews));
        }

        try
        {
            return await RegisterAsync(command, products, stocks, transaction, cancellationToken);
        }
        catch (DomainException ex)
        {
            LogRejected(ex, command.DraftId, command.Lines.Count);
            return Result.Failure<ConfirmedSale>(new ValidationFailed([new FieldError(SaleFields.Lines, ex.Message)]));
        }
    }

    private async Task<Result<ConfirmedSale>> RegisterAsync(
        ConfirmSaleCommand command,
        Dictionary<Guid, Product> products,
        Dictionary<Guid, ProductStock> stocks,
        IWriteTransaction transaction,
        CancellationToken cancellationToken)
    {
        // El Cart valida cantidades, decimales de la unidad e importes; su total es el de la venta.
        var cart = Cart.Restore(
            command.DraftId,
            command.Lines.Select(l =>
            {
                var product = products[l.ProductId];
                var unit = UnitOfMeasure.Find(product.UnitCode) ?? UnitOfMeasure.Default;
                return new CartLine(
                    product.Id,
                    product.Name,
                    product.Sku,
                    unit.Code,
                    unit.DecimalPlaces,
                    product.TracksInventory,
                    product.Price,
                    Quantity.FromThousandths(l.QuantityThousandths));
            }));

        var checkout = BuildCheckout(cart.Total, command.Payments, out var paymentError);
        if (paymentError is not null)
        {
            return Result.Failure<ConfirmedSale>(new ValidationFailed([new FieldError(SaleFields.Payments, paymentError)]));
        }

        if (!checkout.CanConfirm)
        {
            return Result.Failure<ConfirmedSale>(new ValidationFailed(
                [new FieldError(SaleFields.Payments, SaleMessages.PaymentShort)]));
        }

        var folioNumber = await _sales.NextFolioNumberAsync(cancellationToken);
        var folio = Folio.Format(folioNumber);

        var lines = new List<SaleLine>();
        foreach (var (line, position) in cart.Lines.Select((l, i) => (l, i + 1)))
        {
            Guid? movementId = null;
            if (line.TracksInventory)
            {
                if (!stocks.TryGetValue(line.ProductId, out var stock))
                {
                    stock = ProductStock.Start(line.ProductId);
                    stocks[line.ProductId] = stock;
                    _inventory.AddStock(stock);
                }

                var unit = UnitOfMeasure.Find(line.UnitCode) ?? UnitOfMeasure.Default;
                var movement = stock.RecordSale(line.Quantity, unit, folio);
                _inventory.AddMovement(movement);
                movementId = movement.Id;
            }

            lines.Add(SaleLine.Create(
                position,
                line.ProductId,
                line.Name,
                line.Sku,
                line.UnitCode,
                line.DecimalPlaces,
                line.UnitPrice,
                line.Quantity,
                movementId));
        }

        var sale = Sale.Register(
            folioNumber,
            command.DraftId,
            lines,
            checkout.ToPayments().Select(SalePayment.Create));
        _sales.Add(sale);
        _drafts.Remove();

        var outcome = await _sales.SaveChangesAsync(cancellationToken);
        if (outcome.Status != SaveStatus.Saved)
        {
            return await ResolveSaveFailureAsync(command, outcome, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        LogRegistered(sale.Id, folio, sale.Lines.Count, sale.TotalCents);
        return Result.Success(new ConfirmedSale(sale.Id, folio, sale.TotalCents, checkout.Change.Cents));
    }

    private static Checkout BuildCheckout(Money total, IReadOnlyList<PaymentInput> payments, out string? error)
    {
        var checkout = new Checkout(total);
        error = null;
        try
        {
            foreach (var payment in payments)
            {
                if (payment.Method == PaymentMethod.Cash)
                {
                    checkout.SetCashReceived(Money.FromCents(payment.ReceivedCents ?? 0));
                }
                else
                {
                    checkout.AddNonCash(payment.Method, Money.FromCents(payment.AmountCents), payment.Reference);
                }
            }
        }
        catch (DomainException ex)
        {
            error = ex.Message;
        }

        return checkout;
    }

    /// <summary>
    /// Un conflicto o un <c>DraftId</c> duplicado significa que otra operación ya registró la venta:
    /// se devuelve esa venta. Un folio duplicado o cualquier otro conflicto se reintenta desde la interfaz.
    /// </summary>
    private async Task<Result<ConfirmedSale>> ResolveSaveFailureAsync(
        ConfirmSaleCommand command,
        SaveOutcome outcome,
        CancellationToken cancellationToken)
    {
        LogConflict(command.DraftId, command.Lines.Count);
        var byDraft = outcome.Status == SaveStatus.Conflict || outcome.DuplicateField == SaleFields.DraftId;
        if (byDraft && await _sales.FindByDraftAsync(command.DraftId, cancellationToken) is { } existing)
        {
            return Result.Failure<ConfirmedSale>(new AlreadyRegistered(existing.SaleId, existing.Folio));
        }

        return Result.Failure<ConfirmedSale>(new Conflict());
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Venta registrada. SaleId={SaleId} Folio={Folio} Lines={Lines} TotalCents={TotalCents}")]
    private partial void LogRegistered(Guid saleId, string folio, int lines, long totalCents);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Venta no registrada: cambió un precio o una disponibilidad. DraftId={DraftId} Lines={Lines}")]
    private partial void LogChanged(Guid draftId, int lines);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Venta rechazada por reglas de dominio. DraftId={DraftId} Lines={Lines}")]
    private partial void LogRejected(Exception exception, Guid draftId, int lines);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Venta no registrada por conflicto. DraftId={DraftId} Lines={Lines}")]
    private partial void LogConflict(Guid draftId, int lines);
}
