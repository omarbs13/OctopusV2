using FluentValidation;
using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.CashShifts;
using Pos.Application.CreditNotes;
using Pos.Application.Customers;
using Pos.Application.Discounts;
using Pos.Application.Inventory;
using Pos.Application.Licensing;
using Pos.Application.Printing.Ticket;
using Pos.Application.Products;
using Pos.Application.Receivables;
using Pos.Domain.Audit;
using Pos.Domain.Common;
using Pos.Domain.CreditNotes;
using Pos.Domain.Customers;
using Pos.Domain.Discounts;
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
/// transacción y crea su cuenta por cobrar (research §1–§4). Desde 015 reconstruye el <c>Cart</c> con los
/// descuentos, revalida el límite vigente y las aprobaciones, revalida y consume el cupón, reparte el
/// descuento de venta entre las líneas y guarda cada descuento aplicado (015, research §4–§9).
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
    private readonly IDiscountSettingsStore? _discountSettings;
    private readonly IDiscountApprovalStore? _approvals;
    private readonly ICouponRepository? _coupons;
    private readonly IClock? _clock;

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
        ICurrentUser? currentUser = null,
        IDiscountSettingsStore? discountSettings = null,
        IDiscountApprovalStore? approvals = null,
        ICouponRepository? coupons = null,
        IClock? clock = null)
    {
        _discountSettings = discountSettings;
        _approvals = approvals;
        _coupons = coupons;
        _clock = clock;
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

        // 015: aplicar descuentos exige el módulo Descuentos y el permiso ApplyDiscounts (FR-021, FR-022).
        if (command.HasDiscounts)
        {
            var discounts = await _access.CheckAsync(Permission.ApplyDiscounts, cancellationToken);
            if (!discounts.Allowed)
            {
                return Result.Failure<ConfirmedSale>(discounts.Error!);
            }

            if (_license?.IsModuleActive(LicensedModule.Discounts) == false
                || _discountSettings is null || _approvals is null || _coupons is null || _currentUser is null)
            {
                return Result.Failure<ConfirmedSale>(new ModuleNotLicensed(LicensedModule.Discounts));
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

        // 015: los descuentos se aplican con las reglas del dominio; uno inválido rechaza la venta (no se descarta).
        var applied = await ApplyDiscountsAsync(command, cart, cancellationToken);
        if (!applied.IsSuccess)
        {
            return Result.Failure<ConfirmedSale>(applied.Error);
        }

        var discounts = applied.Value;

        // 015, research §6: una venta de total 0 no lleva pagos; una con total mayor que 0 sí.
        if (cart.Total.Cents == 0 && command.Payments.Count > 0)
        {
            return Result.Failure<ConfirmedSale>(new ValidationFailed([new FieldError(SaleFields.Payments, SaleMessages.TotalZeroWithoutPayments)]));
        }

        if (cart.Total.Cents > 0 && command.Payments.Count == 0)
        {
            return Result.Failure<ConfirmedSale>(new ValidationFailed([new FieldError(SaleFields.Payments, SaleMessages.PaymentsRequired)]));
        }

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

        var allocation = cart.Allocation();
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
                movementId,
                line.LineDiscountAmount.Cents,
                allocation[position - 1]));
        }

        var saleDiscounts = BuildSaleDiscounts(cart, lines, discounts);
        var sale = Sale.Register(
            folioNumber,
            command.DraftId,
            cashShiftId,
            lines,
            checkout.ToPayments().Select(SalePayment.Create),
            saleDiscounts);
        AuditDiscounts(sale, saleDiscounts);
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
        if (sale.DiscountCents > 0)
        {
            LogDiscounts(sale.Id, folio, _currentUser?.UserId, sale.DiscountCents, discounts.Coupon?.Code);
        }

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

    /// <summary>
    /// Aplica al <paramref name="cart"/> los descuentos del comando y verifica las aprobaciones contra el
    /// límite vigente (research §7): cada descuento manual que lo supera necesita la aprobación indicada por
    /// su <c>ApprovalId</c>, del mismo borrador, solicitante y alcance, que cubra el equivalente actual. No hay
    /// excepción para el Administrador. El cupón se busca y revalida dentro de la transacción y se consume
    /// aquí, así que dos ventas no pueden usar el último uso (research §9).
    /// </summary>
    private async Task<Result<AppliedDiscounts>> ApplyDiscountsAsync(ConfirmSaleCommand command, Cart cart, CancellationToken cancellationToken)
    {
        if (!command.HasDiscounts)
        {
            return Result.Success(AppliedDiscounts.None);
        }

        foreach (var input in command.Lines.Where(l => l.Discount is not null))
        {
            var discount = input.Discount!;
            cart.SetLineDiscount(input.ProductId, new LineDiscount(DiscountValue.Create(discount.Mode, discount.Value), discount.ApprovalId));
        }

        Coupon? coupon = null;
        if (command.OrderDiscount is { IsCoupon: true } couponInput)
        {
            var code = Coupon.NormalizeCode(couponInput.CouponCode);
            var today = DiscountDates.LocalToday(Clock);
            coupon = await _coupons!.FindByCodeAsync(code, cancellationToken);
            var status = coupon?.StatusOn(today);
            if (coupon is null || status != CouponStatus.Active)
            {
                LogCouponRejected(command.DraftId, code, status);
                return Result.Failure<AppliedDiscounts>(new CouponNotValid(
                    coupon?.Code ?? code, status, coupon?.StartsOn ?? today, coupon?.EndsOn ?? today));
            }

            cart.SetOrderDiscount(new OrderDiscount.CouponApplied(coupon.Id, coupon.Code, coupon.Discount));
        }
        else if (command.OrderDiscount is { } manualInput)
        {
            var value = DiscountValue.Create(manualInput.Mode, manualInput.Value);
            if (value.Mode == DiscountMode.Amount && value.Raw > cart.Subtotal.Cents)
            {
                return Result.Failure<AppliedDiscounts>(new OrderDiscountRemoved(value.Raw));
            }

            cart.SetOrderDiscount(new OrderDiscount.Manual(value, manualInput.ApprovalId));
        }

        var limit = _discountSettings!.Load().LimitBasisPoints;
        var userId = _currentUser!.UserId;
        IReadOnlyList<DiscountApproval>? approvals = null;
        var authorizers = new Dictionary<Guid, Guid>();
        Guid? orderAuthorizer = null;

        foreach (var line in cart.Lines.Where(l => l.HasDiscount))
        {
            var amount = line.LineDiscountAmount.Cents;
            if (!DiscountMath.ExceedsLimit(amount, line.Amount.Cents, limit))
            {
                continue;
            }

            approvals ??= await _approvals!.ListForDraftAsync(command.DraftId, userId, cancellationToken);
            var equivalent = DiscountMath.EquivalentBasisPoints(amount, line.Amount.Cents);
            var approval = approvals.SingleOrDefault(a => a.Id == line.Discount!.ApprovalId);
            if (approval is null || !approval.Covers(DiscountScope.Line, line.ProductId, equivalent))
            {
                LogApprovalRequired(command.DraftId, userId, DiscountScope.Line, line.ProductId, amount, equivalent, limit);
                return Result.Failure<AppliedDiscounts>(new DiscountApprovalRequired(DiscountScope.Line, line.ProductId));
            }

            authorizers[line.ProductId] = approval.AuthorizedBy;
        }

        if (cart.OrderDiscount is OrderDiscount.Manual manual
            && DiscountMath.ExceedsLimit(cart.OrderDiscountAmount.Cents, cart.Subtotal.Cents, limit))
        {
            approvals ??= await _approvals!.ListForDraftAsync(command.DraftId, userId, cancellationToken);
            var equivalent = DiscountMath.EquivalentBasisPoints(cart.OrderDiscountAmount.Cents, cart.Subtotal.Cents);
            var approval = approvals.SingleOrDefault(a => a.Id == manual.ApprovalId);
            if (approval is null || !approval.Covers(DiscountScope.Order, null, equivalent))
            {
                LogApprovalRequired(command.DraftId, userId, DiscountScope.Order, null, cart.OrderDiscountAmount.Cents, equivalent, limit);
                return Result.Failure<AppliedDiscounts>(new DiscountApprovalRequired(DiscountScope.Order, null));
            }

            orderAuthorizer = approval.AuthorizedBy;
        }

        // El uso se cuenta al cobrar, en la misma transacción que la venta (FR-014).
        if (coupon is not null && cart.OrderDiscountAmount.Cents > 0)
        {
            coupon.ConsumeUse(DiscountDates.LocalToday(Clock));
        }

        return Result.Success(new AppliedDiscounts(authorizers, orderAuthorizer, coupon));
    }

    /// <summary>Un <see cref="SaleDiscount"/> por descuento aplicado, con aplicador y autorizador (FR-015).</summary>
    private List<SaleDiscount> BuildSaleDiscounts(Cart cart, List<SaleLine> lines, AppliedDiscounts discounts)
    {
        var result = new List<SaleDiscount>();
        if (!cart.HasDiscounts)
        {
            return result;
        }

        var userId = _currentUser!.UserId;
        var now = Clock.UtcNow;
        foreach (var (cartLine, saleLine) in cart.Lines.Zip(lines))
        {
            if (cartLine.Discount is { } discount)
            {
                result.Add(SaleDiscount.ForLine(
                    saleLine.Id,
                    discount.Value,
                    cartLine.LineDiscountAmount.Cents,
                    userId,
                    discounts.LineAuthorizers.TryGetValue(cartLine.ProductId, out var authorizer) ? authorizer : null,
                    now));
            }
        }

        var orderAmount = cart.OrderDiscountAmount.Cents;
        switch (cart.OrderDiscount)
        {
            case OrderDiscount.Manual manual when orderAmount > 0:
                result.Add(SaleDiscount.ForOrder(manual.Value, orderAmount, userId, discounts.OrderAuthorizer, now));
                break;
            case OrderDiscount.CouponApplied coupon when orderAmount > 0:
                result.Add(SaleDiscount.ForCoupon(coupon.CouponId, coupon.Code, coupon.Value, orderAmount, userId, now));
                break;
        }

        return result;
    }

    /// <summary>
    /// Una sola entrada <c>SALE_DISCOUNTS_APPLIED</c> por venta con descuento, autorizado o no, con un cambio
    /// por descuento: importe sin y con descuento, descripción, cupón y autorización (018, research §7).
    /// </summary>
    private void AuditDiscounts(Sale sale, List<SaleDiscount> discounts)
    {
        if (_audit is null || discounts.Count == 0)
        {
            return;
        }

        var changes = new List<AuditFieldChange>();
        foreach (var discount in discounts)
        {
            var scope = discount.Kind == DiscountKind.Line ? DiscountScope.Line : DiscountScope.Order;
            var line = discount.SaleLineId is { } lineId ? sale.Lines.Single(l => l.Id == lineId) : null;
            var baseCents = line?.OriginalAmountCents ?? sale.SubtotalCents;
            var after = $"{TicketBuilder.FormatMoney(baseCents - discount.AmountCents)} · {DiscountTexts.Describe(scope, discount.Discount, discount.AmountCents, baseCents)}";
            if (discount.CouponCode is { } code)
            {
                after += $" · Cupón {code}";
            }

            if (discount.AuthorizedBy is not null)
            {
                after += " · Autorizado por un Administrador";
            }

            changes.Add(new AuditFieldChange(
                line is null ? "Total de la venta" : $"Producto {line.ProductName}",
                TicketBuilder.FormatMoney(baseCents),
                after));
        }

        _audit.Add(new AuditRecord(
            AuditActions.SaleDiscountsApplied,
            AuditActions.SaleEntity,
            sale.Id,
            EntityName: $"Venta {sale.Folio}",
            Changes: changes,
            AuthorizedBy: discounts.FirstOrDefault(d => d.AuthorizedBy is not null)?.AuthorizedBy));
    }

    private IClock Clock => _clock ?? SystemUtcClock.Instance;

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
        Message = "Venta con descuentos. SaleId={SaleId} Folio={Folio} UserId={UserId} DescuentoCents={DiscountCents} Cupon={CouponCode}")]
    private partial void LogDiscounts(Guid saleId, string folio, Guid? userId, long discountCents, string? couponCode);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Venta no registrada: descuento sin aprobación que lo cubra. DraftId={DraftId} UserId={UserId} Alcance={Scope} ProductId={ProductId} MontoCents={AmountCents} EquivalentePb={Equivalent} LimitePb={Limit}")]
    private partial void LogApprovalRequired(Guid draftId, Guid userId, DiscountScope scope, Guid? productId, long amountCents, long equivalent, long limit);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Venta no registrada: el cupón ya no es válido. DraftId={DraftId} Cupon={CouponCode} Estado={Status}")]
    private partial void LogCouponRejected(Guid draftId, string couponCode, CouponStatus? status);

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

/// <summary>Autorizadores de los descuentos que superaron el límite (por producto y de la venta) y el cupón consumido.</summary>
internal sealed record AppliedDiscounts(IReadOnlyDictionary<Guid, Guid> LineAuthorizers, Guid? OrderAuthorizer, Coupon? Coupon)
{
    public static AppliedDiscounts None { get; } = new(new Dictionary<Guid, Guid>(), null, null);
}

/// <summary>Reloj del sistema cuando la composición no inyecta uno (pruebas antiguas); la persistencia fija las fechas.</summary>
internal sealed class SystemUtcClock : IClock
{
    public static SystemUtcClock Instance { get; } = new();

    public DateTime UtcNow => DateTime.UtcNow;
}
