using FluentValidation;
using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.CashShifts;
using Pos.Application.CreditNotes;
using Pos.Application.Customers;
using Pos.Application.Inventory;
using Pos.Application.Licensing;
using Pos.Application.Printing.Ticket;
using Pos.Application.Products;
using Pos.Application.Receivables;
using Pos.Domain.Common;
using Pos.Domain.CreditNotes;
using Pos.Domain.Customers;
using Pos.Domain.Inventory;
using Pos.Domain.Licensing;
using Pos.Domain.Products;
using Pos.Domain.Receivables;
using Pos.Domain.Sales;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Sales.ConfirmSale;

/// <summary>
/// Registra la venta en una sola transacción de escritura (research §4): folio consecutivo, venta,
/// líneas con copia de datos, pagos, movimientos <c>SALE</c> y borrado del borrador. Una falla no
/// consume folio ni deja nada a medias, y el <c>DraftId</c> vuelve idempotente el reintento.
/// Desde 014 una venta con pago <c>ACCOUNT</c> verifica el crédito del cliente dentro de la misma
/// transacción y crea su cuenta por cobrar (research §1–§4).
/// </summary>
public sealed partial class ConfirmSaleHandler
{
    private readonly IAccessControl _access;
    private readonly IProductRepository _products;
    private readonly IInventoryRepository _inventory;
    private readonly ISaleRepository _sales;
    private readonly ISaleDraftStore _drafts;
    private readonly ShiftGuard _shiftGuard;
    private readonly IWriteTransactions _transactions;
    private readonly IValidator<ConfirmSaleCommand> _validator;
    private readonly ILogger<ConfirmSaleHandler> _logger;
    private readonly ILicenseState? _license;
    private readonly ICreditNoteRepository? _creditNotes;
    private readonly IAuditLog? _audit;
    private readonly ICustomerRepository? _customers;
    private readonly IReceivableRepository? _receivables;
    private readonly ICurrentUser? _currentUser;

    public ConfirmSaleHandler(
        IAccessControl access,
        IProductRepository products,
        IInventoryRepository inventory,
        ISaleRepository sales,
        ISaleDraftStore drafts,
        ShiftGuard shiftGuard,
        IWriteTransactions transactions,
        IValidator<ConfirmSaleCommand> validator,
        ILogger<ConfirmSaleHandler> logger,
        ILicenseState? license = null,
        ICreditNoteRepository? creditNotes = null,
        IAuditLog? audit = null,
        ICustomerRepository? customers = null,
        IReceivableRepository? receivables = null,
        ICurrentUser? currentUser = null)
    {
        _customers = customers;
        _receivables = receivables;
        _currentUser = currentUser;
        _license = license;
        _creditNotes = creditNotes;
        _audit = audit;
        _access = access;
        _products = products;
        _inventory = inventory;
        _sales = sales;
        _drafts = drafts;
        _shiftGuard = shiftGuard;
        _transactions = transactions;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<ConfirmedSale>> HandleAsync(ConfirmSaleCommand command, CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(Permission.Sell, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<ConfirmedSale>(access.Error!);
        }

        ArgumentNullException.ThrowIfNull(command);

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<ConfirmedSale>(ProductRules.ToError(validation));
        }

        // 014: vender a crédito exige el módulo Crédito y clientes y el permiso SellOnCredit.
        if (command.Payments.Any(p => p.Method == PaymentMethod.OnAccount))
        {
            var credit = await _access.CheckAsync(Permission.SellOnCredit, cancellationToken);
            if (!credit.Allowed)
            {
                return Result.Failure<ConfirmedSale>(credit.Error!);
            }

            if (_customers is null || _receivables is null)
            {
                return Result.Failure<ConfirmedSale>(new ModuleNotLicensed(LicensedModule.CreditAndCustomers));
            }
        }

        await using var transaction = await _transactions.BeginAsync(cancellationToken);

        if (await _sales.FindByDraftAsync(command.DraftId, cancellationToken) is { } existing)
        {
            return Result.Failure<ConfirmedSale>(new AlreadyRegistered(existing.SaleId, existing.Folio));
        }

        // 008: solo el dueño del turno abierto vende; se verifica dentro de la transacción (research §5).
        // 012: con Turnos sin licencia la venta se registra sin turno y sin error (FR-021).
        Guid? shiftId = null;
        if (_license?.IsModuleActive(LicensedModule.CashShifts) != false)
        {
            var shiftResult = await _shiftGuard.RequireOwnOpenShiftAsync(cancellationToken);
            if (!shiftResult.IsSuccess)
            {
                return Result.Failure<ConfirmedSale>(shiftResult.Error);
            }

            shiftId = shiftResult.Value.Id;
        }

        // 012: con Inventario sin licencia no se validan existencias ni se generan movimientos.
        var inventoryActive = _license?.IsModuleActive(LicensedModule.Inventory) != false;

        var ids = command.Lines.Select(l => l.ProductId).ToList();
        var products = (await _products.GetManyAsync(ids, includeDeleted: true, cancellationToken)).ToDictionary(p => p.Id);
        var tracked = inventoryActive
            ? products.Values.Where(p => p.TracksInventory).Select(p => p.Id).ToList()
            : [];
        var stocks = tracked.Count == 0
            ? []
            : (await _inventory.GetStocksAsync(tracked, cancellationToken)).ToDictionary(s => s.Key, s => s.Value);

        var reviews = command.Lines
            .Select(l => SaleReviewer.Review(l.ProductId, l.QuantityThousandths, products.GetValueOrDefault(l.ProductId), stocks, inventoryActive))
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
            return await RegisterAsync(command, shiftId, inventoryActive, products, stocks, transaction, cancellationToken);
        }
        catch (DomainException ex)
        {
            LogRejected(ex, command.DraftId, command.Lines.Count);
            return Result.Failure<ConfirmedSale>(new ValidationFailed([new FieldError(SaleFields.Lines, ex.Message)]));
        }
    }

    private async Task<Result<ConfirmedSale>> RegisterAsync(
        ConfirmSaleCommand command,
        Guid? cashShiftId,
        bool inventoryActive,
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

        // 014: crédito del cliente con el saldo leído dentro de la transacción (research §4). La concesión
        // de exceder el límite se consume aquí, al final de las validaciones.
        CreditApproval? credit = null;
        if (checkout.IsOnAccount)
        {
            var approval = await ApproveCreditAsync(command, checkout.Total.Cents, transaction, cancellationToken);
            if (!approval.IsSuccess)
            {
                return Result.Failure<ConfirmedSale>(approval.Error);
            }

            credit = approval.Value;
        }

        // Pago con nota de crédito (013, Historia 4): se resuelve la nota y su saldo antes de tocar inventario.
        CreditNote? creditNote = null;
        long creditBalance = 0;
        var creditEntry = checkout.Payments.FirstOrDefault(p => p.Method == PaymentMethod.CreditNote);
        if (creditEntry is not null)
        {
            if (_creditNotes is null || _license?.IsModuleActive(LicensedModule.Returns) == false)
            {
                return Result.Failure<ConfirmedSale>(new ModuleNotLicensed(LicensedModule.Returns));
            }

            if (!CreditNoteFolio.TryParse(creditEntry.Reference, out var noteNumber)
                || await _creditNotes.FindByNumberAsync(noteNumber, cancellationToken) is not { } found)
            {
                return Result.Failure<ConfirmedSale>(new CreditNoteNotFound());
            }

            creditBalance = await _creditNotes.GetBalanceAsync(found.Id, cancellationToken);
            if (creditBalance <= 0)
            {
                return Result.Failure<ConfirmedSale>(new CreditNoteNotFound());
            }

            if (creditEntry.Amount.Cents > creditBalance)
            {
                return Result.Failure<ConfirmedSale>(new InsufficientCreditNote(creditBalance));
            }

            creditNote = found;
        }

        var folioNumber = await _sales.NextFolioNumberAsync(cancellationToken);
        var folio = Folio.Format(folioNumber);

        var lines = new List<SaleLine>();
        foreach (var (line, position) in cart.Lines.Select((l, i) => (l, i + 1)))
        {
            Guid? movementId = null;
            if (line.TracksInventory && inventoryActive)
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
            cashShiftId,
            lines,
            checkout.ToPayments().Select(SalePayment.Create));
        if (creditNote is not null)
        {
            await RedeemCreditNoteAsync(sale, creditNote, creditBalance, cancellationToken);
        }

        if (credit is not null)
        {
            RegisterReceivable(sale, credit);
        }

        _sales.Add(sale);
        _drafts.Remove();

        var outcome = await _sales.SaveChangesAsync(cancellationToken);
        if (outcome.Status != SaveStatus.Saved)
        {
            return await ResolveSaveFailureAsync(command, outcome, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        LogRegistered(sale.Id, folio, sale.Lines.Count, sale.TotalCents);
        if (credit is not null)
        {
            LogCreditSale(sale.Id, folio, credit.Customer.Id, _currentUser?.UserId, sale.TotalCents, credit.BalanceCents, credit.Customer.CreditLimitCents, credit.AuthorizedBy);
        }

        return Result.Success(new ConfirmedSale(sale.Id, folio, sale.TotalCents, checkout.Change.Cents));
    }

    /// <summary>Descuenta del saldo de la nota lo usado como pago y deja la huella en la bitácora.</summary>
    private async Task RedeemCreditNoteAsync(Sale sale, CreditNote note, long balanceCents, CancellationToken cancellationToken)
    {
        var payment = sale.Payments.Single(p => p.Method == PaymentMethod.CreditNote);
        payment.LinkCreditNote(note.Id);
        var sequence = await _creditNotes!.NextMovementSequenceAsync(note.Id, cancellationToken);
        _creditNotes.AddMovement(note.Redeem(payment.AmountCents, balanceCents, sequence, sale.Id));
        _audit?.Add(
            AuditActions.CreditNoteRedeemed,
            AuditActions.CreditNoteEntity,
            note.Id,
            $"Nota {note.Folio}. Venta {sale.Folio}. Monto {TicketBuilder.FormatMoney(payment.AmountCents)}");
    }

    /// <summary>
    /// Cliente con crédito y límite (FR-006): <c>saldo + total ≤ límite</c>. Si se excede, el
    /// Administrador pasa directo y queda como autorizador; un Cajero necesita la concesión. Una concesión
    /// inválida deja <c>ADMIN_AUTHORIZATION_DENIED</c> en la bitácora (sin contraseña) y no registra la venta.
    /// </summary>
    private async Task<Result<CreditApproval>> ApproveCreditAsync(
        ConfirmSaleCommand command,
        long totalCents,
        IWriteTransaction transaction,
        CancellationToken cancellationToken)
    {
        var customer = command.CustomerId is { } customerId ? await _customers!.GetAsync(customerId, cancellationToken) : null;
        if (customer is not { CanBuyOnCredit: true })
        {
            LogNotEligible(command.DraftId, command.CustomerId);
            return Result.Failure<CreditApproval>(new CustomerNotEligibleForCredit());
        }

        var balance = await _customers!.GetBalanceAsync(customer.Id, cancellationToken);
        var check = CreditPolicy.Check(balance, customer.CreditLimitCents, totalCents);
        if (!check.IsExceeded)
        {
            return Result.Success(new CreditApproval(customer, balance, check, null));
        }

        var approval = await _access.CheckAsync(Permission.ApproveCreditOverLimit, command.OverLimitGrantId, cancellationToken);
        if (!approval.Allowed)
        {
            if (command.OverLimitGrantId is not null && _audit is not null)
            {
                // Solo la bitácora: nada más cambió todavía en la transacción.
                _audit.Add(
                    AuditActions.AdminAuthorizationDenied,
                    AuditActions.CustomerEntity,
                    customer.Id,
                    $"Permiso: {Permission.ApproveCreditOverLimit}. Concesión inválida o vencida. Cliente: {customer.Name}. Excedente {TicketBuilder.FormatMoney(check.ExcessCents)}");
                await _audit.SaveAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }

            LogOverLimit(command.DraftId, customer.Id, balance, customer.CreditLimitCents, totalCents, check.ExcessCents);
            return Result.Failure<CreditApproval>(new CreditLimitExceeded(check.ExcessCents));
        }

        // El Administrador tiene el permiso y no se le pide su contraseña otra vez: él es el autorizador (research §4).
        return Result.Success(new CreditApproval(customer, balance, check, approval.AuthorizedBy ?? _currentUser?.UserId));
    }

    /// <summary>Cuenta por cobrar de la venta y su huella en la bitácora (FR-007).</summary>
    private void RegisterReceivable(Sale sale, CreditApproval credit)
    {
        _receivables!.Add(Receivable.Create(sale.Id, credit.Customer.Id, credit.Customer.Name, sale.TotalCents, credit.AuthorizedBy));
        var figures = $"Cliente: {credit.Customer.Name}. Venta {sale.Folio}. Saldo previo {TicketBuilder.FormatMoney(credit.BalanceCents)}. "
            + $"Límite {TicketBuilder.FormatMoney(credit.Customer.CreditLimitCents)}. Monto {TicketBuilder.FormatMoney(sale.TotalCents)}";
        _audit?.Add(AuditActions.CreditSaleRegistered, AuditActions.SaleEntity, sale.Id, figures);
        if (credit.Check.IsExceeded)
        {
            _audit?.Add(
                AuditActions.CreditLimitOverride,
                AuditActions.SaleEntity,
                sale.Id,
                $"{figures}. Excedente {TicketBuilder.FormatMoney(credit.Check.ExcessCents)}",
                credit.AuthorizedBy);
        }
    }

    private static Checkout BuildCheckout(Money total, IReadOnlyList<PaymentInput> payments, out string? error)
    {
        var checkout = new Checkout(total);
        error = null;
        try
        {
            foreach (var payment in payments)
            {
                if (payment.Method == PaymentMethod.OnAccount)
                {
                    // Un único pago por el 100 % del total, sin recibido ni cambio (FR-005).
                    if (payment.AmountCents != total.Cents)
                    {
                        error = SaleMessages.OnAccountExclusive;
                        return checkout;
                    }

                    checkout.SetOnAccount();
                }
                else if (payment.Method == PaymentMethod.Cash)
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
        Message = "Venta a crédito registrada. SaleId={SaleId} Folio={Folio} CustomerId={CustomerId} UserId={UserId} TotalCents={TotalCents} SaldoPrevioCents={BalanceCents} LimiteCents={LimitCents} AutorizadoPor={AuthorizedBy}")]
    private partial void LogCreditSale(Guid saleId, string folio, Guid customerId, Guid? userId, long totalCents, long balanceCents, long limitCents, Guid? authorizedBy);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Venta a crédito rechazada: excede el límite sin autorización. DraftId={DraftId} CustomerId={CustomerId} SaldoCents={BalanceCents} LimiteCents={LimitCents} TotalCents={TotalCents} ExcedenteCents={ExcessCents}")]
    private partial void LogOverLimit(Guid draftId, Guid customerId, long balanceCents, long limitCents, long totalCents, long excessCents);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Venta a crédito rechazada: el cliente no tiene crédito. DraftId={DraftId} CustomerId={CustomerId}")]
    private partial void LogNotEligible(Guid draftId, Guid? customerId);

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

/// <summary>Crédito aprobado para la venta: cliente, saldo previo, verificación del límite y autorizador del excedente.</summary>
internal sealed record CreditApproval(Customer Customer, long BalanceCents, CreditCheck Check, Guid? AuthorizedBy);
