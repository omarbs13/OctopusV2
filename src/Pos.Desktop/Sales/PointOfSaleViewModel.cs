using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.CashShifts;
using Pos.Application.CashShifts.GetCurrentShift;
using Pos.Application.Inventory;
using Pos.Application.Sales;
using Pos.Application.Sales.ConfirmSale;
using Pos.Application.Sales.DiscardSaleDraft;
using Pos.Application.Sales.FindProductsForSale;
using Pos.Application.Sales.GetSaleDraft;
using Pos.Application.Sales.ReviewSale;
using Pos.Application.Sales.SaveSaleDraft;
using Pos.Desktop.Auth;
using Pos.Desktop.CashShifts;
using Pos.Desktop.Common;
using Pos.Desktop.Diagnostics;
using Pos.Desktop.Resources;
using Pos.Desktop.Settings;
using Pos.Domain.CashShifts;
using Pos.Domain.Common;
using Pos.Domain.Products;
using Pos.Domain.Sales;
using Pos.Domain.Users;
using Serilog;

namespace Pos.Desktop.Sales;

/// <summary>Línea de la venta en curso con los textos ya formateados.</summary>
public sealed record CartLineRow(CartLine Line)
{
    public Guid ProductId => Line.ProductId;

    public string Name => Line.Name;

    public string Sku => Line.Sku;

    public string QuantityText
    {
        get
        {
            var number = QuantityConverter.Format(Line.Quantity.Thousandths, Line.DecimalPlaces);
            var unit = UnitOfMeasure.Find(Line.UnitCode);
            return unit is null || unit.Code == UnitOfMeasure.Piece.Code ? number : $"{number} {unit.Name}";
        }
    }

    public string PriceText => MoneyConverter.Format(Line.UnitPrice.Cents);

    public string AmountText => MoneyConverter.Format(Line.Amount.Cents);

    public bool IsUnavailable => Line.IsUnavailable;

    public string UnavailableText => Line.UnavailableReason switch
    {
        UnavailableReason.Deleted => Strings.Sale_LineDeleted,
        UnavailableReason.Inactive => Strings.Sale_LineInactive,
        _ => string.Empty,
    };
}

/// <summary>
/// Punto de venta: captura la venta en curso y abre el cobro. Solo presenta: los importes y el total
/// vienen de <see cref="Cart"/> y <see cref="Checkout"/> del dominio (Principio III), y toda llamada
/// a casos de uso pasa por <see cref="OperationRunner"/>.
/// </summary>
public sealed partial class PointOfSaleViewModel : PageViewModel, IDisposable
{
    /// <summary>Tiempo que se muestra un aviso en la barra (contracts/ui.md).</summary>
    public static readonly TimeSpan StatusDuration = TimeSpan.FromSeconds(4);

    private static readonly CultureInfo Display = CultureInfo.GetCultureInfo("es-MX");

    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly IDialogService _dialogs;
    private readonly ILogger _logger;
    private readonly DraftAutosaver _autosaver;
    private readonly DiagnosticContext? _diagnostics;
    private readonly ScanQueue _scans;
    private readonly TicketPrintingService _printing;
    private readonly AdminAuthorizationService? _authorization;
    private readonly CashShiftDialogs? _shiftDialogs;
    private readonly ICurrentPermissions? _permissions;

    private CheckoutViewModel? _pendingCheckout;
    private bool _draftChecked;
    private int _statusVersion;

    public PointOfSaleViewModel(
        UseCases useCases,
        OperationRunner runner,
        IDialogService dialogs,
        ILogger logger,
        TicketPrintingService printing,
        AdminAuthorizationService? authorization = null,
        CashShiftDialogs? shiftDialogs = null,
        ICurrentPermissions? permissions = null,
        DiagnosticContext? diagnostics = null)
    {
        _diagnostics = diagnostics;
        _authorization = authorization;
        _shiftDialogs = shiftDialogs;
        _permissions = permissions;
        _printing = printing;
        _useCases = useCases;
        _runner = runner;
        _dialogs = dialogs;
        _logger = logger;
        _autosaver = new DraftAutosaver(SaveDraftAsync, logger);
        _scans = new ScanQueue(ProcessCodeAsync, ex => logger.Error(ex, "Error al procesar una lectura del Punto de venta"));
        _scans.Start();
        Cart = new Cart();
        RefreshCart();
    }

    public override string Title => Strings.Nav_PointOfSale;

    /// <summary>Venta en curso (dominio). Sustituida al recuperar el borrador.</summary>
    public Cart Cart { get; private set; }

    public ObservableCollection<CartLineRow> Lines { get; } = [];

    /// <summary>La vista lleva el foco al campo de captura.</summary>
    public event EventHandler? FocusCaptureRequested;

    /// <summary>La vista lleva el foco al cuadro de cantidad.</summary>
    public event EventHandler? FocusQuantityRequested;

    [ObservableProperty]
    public partial string CaptureText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ChangeQuantityCommand), nameof(RemoveLineCommand))]
    public partial CartLineRow? SelectedLine { get; set; }

    [ObservableProperty]
    public partial string TotalText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string ItemsText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasLines { get; private set; }

    [NotifyCanExecuteChangedFor(nameof(CheckoutCommand))]
    [ObservableProperty]
    public partial bool CanCheckout { get; private set; }

    [ObservableProperty]
    public partial bool HasUnavailableLines { get; private set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; private set; }

    [ObservableProperty]
    public partial bool StatusIsWarning { get; private set; }

    /// <summary>Resultado de la última venta, hasta el siguiente escaneo o Enter.</summary>
    [ObservableProperty]
    public partial string? LastSaleText { get; private set; }

    [ObservableProperty]
    public partial string? LastChangeText { get; private set; }

    [ObservableProperty]
    public partial ProductChooserViewModel? Chooser { get; private set; }

    [ObservableProperty]
    public partial CheckoutViewModel? Checkout { get; private set; }

    /// <summary>Diálogo del motivo para abrir el cajón sin venta, mientras esté abierto.</summary>
    [ObservableProperty]
    public partial DrawerReasonViewModel? DrawerReason { get; private set; }

    [ObservableProperty]
    public partial bool IsEditingQuantity { get; private set; }

    [ObservableProperty]
    public partial string QuantityEditText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? QuantityEditError { get; private set; }

    /// <summary>Hay una ventana modal sobre la venta (selector o cobro): los atajos de captura no aplican.</summary>
    public bool IsModalOpen => Chooser is not null || Checkout is not null || DrawerReason is not null;

    public bool HasLastSale => LastSaleText is not null;

    /// <summary>Estado del turno de la caja (008): sin turno, de otro usuario, propio o aún sin consultar.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShiftNone), nameof(IsShiftOther), nameof(IsShiftOwn), nameof(IsSaleBlocked))]
    public partial ShiftViewState ShiftState { get; private set; } = ShiftViewState.Unknown;

    [ObservableProperty]
    public partial CurrentShiftSummary? CurrentShift { get; private set; }

    /// <summary>"Turno T-000123 · desde 08:15 · 23 ventas · $4,560.00": sin fondo, esperado ni movimientos (FR-022).</summary>
    [ObservableProperty]
    public partial string ShiftBarText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string ShiftOtherText { get; private set; } = string.Empty;

    public bool IsShiftNone => ShiftState == ShiftViewState.None;

    public bool IsShiftOther => ShiftState == ShiftViewState.Other;

    public bool IsShiftOwn => ShiftState == ShiftViewState.Own;

    /// <summary>Sin turno propio no se puede vender: el carrito se reemplaza por el panel de turno.</summary>
    public bool IsSaleBlocked => IsShiftNone || IsShiftOther;

    /// <summary>Solo un administrador puede cerrar el turno de otro usuario (FR-024).</summary>
    public bool CanCloseOtherShift => _permissions?.Has(Permission.ManageShifts) ?? false;

    public override async Task OnActivatedAsync()
    {
        await RefreshShiftAsync();
        FocusCaptureRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Consulta el turno de la caja y presenta uno de los tres estados (contracts/ui.md).</summary>
    public async Task RefreshShiftAsync()
    {
        var (completed, result) = await _runner.RunAsync(
            "ConsultarTurnoActual",
            () => _useCases.RunAsync<GetCurrentShiftHandler, Result<CurrentShiftSummary?>>(h => h.HandleAsync(CancellationToken.None)));
        if (completed && result is { IsSuccess: true })
        {
            ApplyShift(result.Value);
        }

        // La venta conservada se ofrece solo cuando ya se puede vender (con turno propio).
        if (!_draftChecked && !IsSaleBlocked)
        {
            _draftChecked = true;
            await OfferDraftRecoveryAsync();
        }
    }

    private void ApplyShift(CurrentShiftSummary? shift)
    {
        CurrentShift = shift;
        if (shift is null)
        {
            ShiftBarText = string.Empty;
            ShiftOtherText = string.Empty;
            ShiftState = ShiftViewState.None;
        }
        else if (shift.IsMine)
        {
            ShiftBarText = string.Format(
                Display,
                Strings.Shift_Bar,
                shift.Folio,
                shift.OpenedAtUtc.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture),
                shift.SalesCount == 1 ? Strings.Shift_SalesOne : string.Format(Display, Strings.Shift_SalesMany, shift.SalesCount),
                MoneyConverter.Format(shift.TotalSoldCents));
            ShiftOtherText = string.Empty;
            ShiftState = ShiftViewState.Own;
        }
        else
        {
            ShiftBarText = string.Empty;
            ShiftOtherText = string.Format(
                Display,
                Strings.Shift_OtherText,
                shift.OpenedByName,
                shift.OpenedAtUtc.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture));
            ShiftState = ShiftViewState.Other;
        }

        UpdateCanCheckout();
        FocusCaptureRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task OpenShiftAsync()
    {
        if (_shiftDialogs is null || IsModalOpen)
        {
            return;
        }

        if (await _shiftDialogs.OpenShiftAsync())
        {
            await RefreshShiftAsync();
        }
    }

    [RelayCommand]
    private Task DepositAsync() => RegisterMovementAsync(CashMovementType.In);

    [RelayCommand]
    private Task WithdrawalAsync() => RegisterMovementAsync(CashMovementType.Out);

    private async Task RegisterMovementAsync(CashMovementType type)
    {
        if (_shiftDialogs is null || IsModalOpen || CurrentShift is not { IsMine: true } shift)
        {
            return;
        }

        if (await _shiftDialogs.RegisterMovementAsync(shift.ShiftId, type))
        {
            await RefreshShiftAsync();
        }
    }

    /// <summary>Cerrar turno propio: espera el guardado pendiente del borrador antes de contar (research §9).</summary>
    [RelayCommand]
    private async Task CloseShiftAsync()
    {
        if (_shiftDialogs is null || IsModalOpen || CurrentShift is not { IsMine: true } shift)
        {
            return;
        }

        if (await _shiftDialogs.CloseShiftAsync(shift.ShiftId, ownerName: null, beforeCount: FlushDraftAsync))
        {
            await RefreshShiftAsync();
        }
    }

    /// <summary>Un administrador cierra el turno de otro usuario desde el panel de turno ajeno (Historia 1, escenario 5).</summary>
    [RelayCommand]
    private async Task CloseOtherShiftAsync()
    {
        if (_shiftDialogs is null || IsModalOpen || !CanCloseOtherShift || CurrentShift is not { IsMine: false } shift)
        {
            return;
        }

        if (await _shiftDialogs.CloseShiftAsync(shift.ShiftId, shift.OpenedByName))
        {
            await RefreshShiftAsync();
        }
    }

    /// <summary>Salir no pregunta: la venta queda en el borrador. Solo espera a que termine el guardado.</summary>
    public override async Task<bool> CanLeaveAsync()
    {
        await _autosaver.FlushAsync();
        Checkout = null;
        Chooser = null;
        return true;
    }

    /// <summary>Espera a que termine la escritura pendiente del borrador (cambio de usuario, cierre de sesión).</summary>
    public Task FlushDraftAsync() => _autosaver.FlushAsync();

    public void Dispose() => _scans.Dispose();

    partial void OnChooserChanged(ProductChooserViewModel? value) => OnModalChanged();

    partial void OnCheckoutChanged(CheckoutViewModel? value) => OnModalChanged();

    partial void OnDrawerReasonChanged(DrawerReasonViewModel? value) => OnModalChanged();

    partial void OnLastSaleTextChanged(string? value) => OnPropertyChanged(nameof(HasLastSale));

    partial void OnShiftStateChanged(ShiftViewState value) => UpdateCanCheckout();

    private void OnModalChanged()
    {
        OnPropertyChanged(nameof(IsModalOpen));
        UpdateCanCheckout();
        if (!IsModalOpen)
        {
            FocusCaptureRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Enter en el campo: toma el texto y lo encola. Con el campo vacío solo cierra el resultado anterior.</summary>
    [RelayCommand]
    private void Capture()
    {
        var text = CaptureText.Trim();
        CaptureText = string.Empty;
        if (text.Length == 0)
        {
            DismissLastSale();
            return;
        }

        _scans.Enqueue(text);
    }

    /// <summary>F2: con texto, busca (abre el selector si no hay coincidencia exacta); sin texto, enfoca el campo.</summary>
    [RelayCommand]
    private void Search()
    {
        if (CaptureText.Trim().Length > 0)
        {
            Capture();
        }
        else
        {
            FocusCaptureRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public void MoveSelection(int delta)
    {
        if (Lines.Count == 0)
        {
            return;
        }

        var index = SelectedLine is { } current ? Lines.IndexOf(current) : -1;
        SelectedLine = Lines[Math.Clamp(index + delta, 0, Lines.Count - 1)];
    }

    /// <summary>F4 o <c>*</c>: edita la cantidad de la línea seleccionada.</summary>
    [RelayCommand(CanExecute = nameof(HasSelectedLine))]
    private void ChangeQuantity()
    {
        if (SelectedLine is not { } row || IsModalOpen)
        {
            return;
        }

        QuantityEditText = row.Line.Quantity.ToEditableString(row.Line.DecimalPlaces);
        QuantityEditError = null;
        IsEditingQuantity = true;
        FocusQuantityRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Enter en el cuadro de cantidad: aplica; si es inválida muestra el mensaje y conserva el valor anterior.</summary>
    [RelayCommand]
    private void ApplyQuantity()
    {
        if (SelectedLine is not { } row)
        {
            CancelQuantityEdit();
            return;
        }

        var parsed = Quantity.Parse(QuantityEditText, row.Line.DecimalPlaces);
        if (parsed.Value is not { } quantity)
        {
            var unit = UnitOfMeasure.Find(row.Line.UnitCode);
            QuantityEditError = InventoryMessages.ForQuantity(
                parsed.Error!.Value, unit?.Name ?? string.Empty, row.Line.DecimalPlaces);
            FocusQuantityRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        try
        {
            Cart.SetQuantity(row.ProductId, quantity);
        }
        catch (DomainException ex)
        {
            QuantityEditError = ex.Message;
            FocusQuantityRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        IsEditingQuantity = false;
        QuantityEditError = null;
        RefreshCart(row.ProductId);
        SaveDraft();
        FocusCaptureRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void CancelQuantityEdit()
    {
        IsEditingQuantity = false;
        QuantityEditError = null;
        FocusCaptureRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Supr: quita la línea seleccionada sin confirmar.</summary>
    [RelayCommand(CanExecute = nameof(HasSelectedLine))]
    private void RemoveLine()
    {
        if (SelectedLine is not { } row || IsModalOpen)
        {
            return;
        }

        var index = Lines.IndexOf(row);
        Cart.Remove(row.ProductId);
        RefreshCart(Cart.Lines.Count == 0 ? null : Cart.Lines[Math.Min(index, Cart.Lines.Count - 1)].ProductId);
        SaveDraft();
    }

    /// <summary>F8: cancela la venta en curso, con confirmación.</summary>
    [RelayCommand]
    private async Task CancelSaleAsync()
    {
        if (!Cart.Lines.Any() || IsModalOpen)
        {
            return;
        }

        if (!await _dialogs.ConfirmAsync(Strings.Sale_CancelTitle, Strings.Sale_CancelQuestion, Strings.Sale_CancelConfirm))
        {
            return;
        }

        StartNewSale();
        ShowStatus(Strings.Sale_Cancelled, warning: false);
    }

    /// <summary>Abre el cajón sin venta: pide el motivo, que queda en la bitácora.</summary>
    [RelayCommand]
    private void OpenDrawer()
    {
        if (IsModalOpen)
        {
            return;
        }

        DrawerReason = new DrawerReasonViewModel(
            _printing,
            (message, warning) => ShowStatus(message, warning),
            () => DrawerReason = null,
            _authorization,
            _dialogs);
    }

    /// <summary>F12: revisa precios y existencia y abre el cobro.</summary>
    [RelayCommand(CanExecute = nameof(CanCheckout))]
    private async Task CheckoutAsync()
    {
        if (!Cart.CanCheckout || IsModalOpen)
        {
            return;
        }

        DismissLastSale();
        await _autosaver.FlushAsync();

        var review = await ReviewAsync();
        if (review is null)
        {
            return;
        }

        Cart.ApplyCurrentPrices(review.Lines.Select(l => new CartPriceUpdate(
            l.ProductId,
            Money.FromCents(l.CurrentPriceCents),
            ToUnavailable(l.NotSellableReason))));
        RefreshCart();
        SaveDraft();

        if (!Cart.CanCheckout)
        {
            ShowStatus(Strings.Sale_RemoveMarkedLines, warning: true);
            return;
        }

        var insufficient = review.Lines.Where(l => l.InsufficientStock).ToList();
        foreach (var line in insufficient)
        {
            _logger.Warning(
                "Existencia insuficiente al cobrar; la venta dejaría la existencia negativa. ProductId={ProductId} OnHandThousandths={OnHand}",
                line.ProductId,
                line.OnHandThousandths);
        }

        if (insufficient.Count > 0 && !await ConfirmStockWarningAsync(insufficient))
        {
            return;
        }

        OpenCheckout();
    }

    private async Task<SaleReview?> ReviewAsync()
    {
        var query = new ReviewSaleQuery([.. Cart.Lines.Select(l => new ReviewLineInput(l.ProductId, l.Quantity.Thousandths))]);
        var (completed, result) = await _runner.RunAsync(
            "RevisarVenta",
            () => _useCases.RunAsync<ReviewSaleHandler, Result<SaleReview>>(h => h.HandleAsync(query, CancellationToken.None)),
            new Dictionary<string, object?> { ["Lines"] = query.Lines.Count });
        return completed && result is { IsSuccess: true } ? result.Value : null;
    }

    private async Task<bool> ConfirmStockWarningAsync(IReadOnlyList<SaleLineReview> shortLines)
    {
        var names = string.Join(", ", shortLines.Select(l => Cart.Lines.Single(c => c.ProductId == l.ProductId).Name));
        return await _dialogs.ConfirmAsync(
            Strings.Sale_StockWarningTitle,
            string.Format(Display, Strings.Sale_StockWarning, names),
            Strings.Sale_StockWarningConfirm);
    }

    private void OpenCheckout()
    {
        // Los pagos capturados se conservan mientras no cambie el total (US3, escenario 7).
        if (_pendingCheckout is null || _pendingCheckout.Total != Cart.Total)
        {
            _pendingCheckout = new CheckoutViewModel(new Domain.Sales.Checkout(Cart.Total), ConfirmSaleAsync, CloseCheckout);
        }

        Checkout = _pendingCheckout;
        _pendingCheckout.RequestFocus();
    }

    private void CloseCheckout() => Checkout = null;

    private async Task ConfirmSaleAsync(CheckoutViewModel checkout)
    {
        await _autosaver.FlushAsync();

        var command = new ConfirmSaleCommand(
            Cart.DraftId,
            [.. Cart.Lines.Select(l => new ConfirmLineInput(l.ProductId, l.Quantity.Thousandths, l.UnitPrice.Cents))],
            [.. checkout.ToPayments().Select(p => new PaymentInput(p.Method, p.Amount.Cents, p.Received?.Cents, p.Reference))]);
        var hadCash = command.Payments.Any(p => p.Method == PaymentMethod.Cash);

        var (completed, result) = await _runner.RunAsync(
            "ConfirmarVenta",
            () => _useCases.RunAsync<ConfirmSaleHandler, Result<ConfirmedSale>>(h => h.HandleAsync(command, CancellationToken.None)),
            new Dictionary<string, object?> { ["DraftId"] = command.DraftId, ["Lines"] = command.Lines.Count });

        if (!completed || result is null)
        {
            checkout.ErrorMessage = Strings.Sale_NotRegistered;
            return;
        }

        switch (result.Error)
        {
            case null:
                OnSaleRegistered(result.Value.Folio, result.Value.ChangeCents);
                StartDevices(result.Value.SaleId, result.Value.Folio, hadCash);
                break;

            case AlreadyRegistered registered:
                OnSaleRegistered(registered.Folio, changeCents: 0);
                break;

            case SaleChanged changed:
                CloseCheckout();
                _pendingCheckout = null;
                ApplyReview(changed.Lines);
                ShowStatus(Strings.Sale_Changed, warning: true);
                break;

            case ShiftRequired or ShiftOwnedByOther:
                // Otro usuario o un administrador cerró o cambió el turno: el carrito sigue en el borrador (research §14).
                CloseCheckout();
                ShowStatus(
                    result.Error is ShiftOwnedByOther other
                        ? CashShiftMessages.ShiftOwnedByOther(other.OpenedByName)
                        : CashShiftMessages.ShiftRequired,
                    warning: true);
                await RefreshShiftAsync();
                break;

            case ValidationFailed validation:
                checkout.ErrorMessage = validation.Errors is [{ } first, ..] ? first.Message : Strings.Sale_NotRegistered;
                break;

            case LicenseExpired expired:
                checkout.ErrorMessage = Licensing.LicenseMessages.Expired(expired);
                break;

            default:
                checkout.ErrorMessage = Strings.Sale_NotRegistered;
                break;
        }
    }

    /// <summary>
    /// Cajón e impresión después de confirmar la venta y fuera de su flujo: el Punto de venta ya quedó
    /// listo y una falla del dispositivo nunca toca la venta (006, FR-009).
    /// </summary>
    private void StartDevices(Guid saleId, string folio, bool hadCash) =>
        _printing.OnSaleRegistered(saleId, folio, hadCash);

    private void ApplyReview(IReadOnlyList<SaleLineReview> lines)
    {
        Cart.ApplyCurrentPrices(lines.Select(l => new CartPriceUpdate(
            l.ProductId,
            Money.FromCents(l.CurrentPriceCents),
            ToUnavailable(l.NotSellableReason))));
        RefreshCart();
        SaveDraft();
    }

    private void OnSaleRegistered(string folio, long changeCents)
    {
        _pendingCheckout = null;
        CloseCheckout();
        StartNewSale();
        LastSaleText = string.Format(Display, Strings.Sale_Registered, folio);
        LastChangeText = string.Format(Display, Strings.Sale_ChangeGiven, MoneyConverter.Format(changeCents));
        _logger.Information("Venta mostrada al operador. Folio={Folio}", folio);
        _ = RefreshShiftAsync();
    }

    /// <summary>Vacía la venta con un borrador nuevo; el guardado sin líneas descarta el borrador anterior.</summary>
    private void StartNewSale()
    {
        Cart.Clear();
        _pendingCheckout = null;
        IsEditingQuantity = false;
        RefreshCart();
        SaveDraft();
    }

    private void DismissLastSale()
    {
        LastSaleText = null;
        LastChangeText = null;
    }

    private async Task ProcessCodeAsync(string text)
    {
        if (IsSaleBlocked)
        {
            return;
        }

        DismissLastSale();
        var (completed, result) = await _runner.RunAsync(
            "BuscarProductoParaVender",
            () => _useCases.RunAsync<FindProductsForSaleHandler, Result<ProductLookup>>(
                h => h.HandleAsync(new FindProductsForSaleQuery(text), CancellationToken.None)),
            new Dictionary<string, object?> { ["Text"] = text });

        if (!completed || result is not { IsSuccess: true })
        {
            return;
        }

        var lookup = result.Value;
        switch (lookup.Kind)
        {
            case LookupKind.ExactMatch:
                AddProduct(lookup.Items[0]);
                break;

            case LookupKind.NameMatches:
                Chooser = new ProductChooserViewModel(
                    lookup.Items,
                    chosen =>
                    {
                        Chooser = null;
                        AddProduct(chosen);
                    },
                    () => Chooser = null);
                break;

            default:
                ShowStatus(string.Format(Display, Strings.Sale_ProductNotFound, text), warning: true);
                break;
        }
    }

    private void AddProduct(SaleProductDto product)
    {
        if (product.NotSellableReason is { } reason)
        {
            ShowStatus(Cart.NotSellableMessage(product.Name, ToUnavailable(reason)!.Value), warning: true);
            return;
        }

        try
        {
            Cart.Add(new CartProduct(
                product.Id,
                product.Name,
                product.Sku,
                product.UnitCode,
                product.DecimalPlaces,
                product.TracksInventory,
                Money.FromCents(product.PriceCents)));
        }
        catch (DomainException ex)
        {
            ShowStatus(ex.Message, warning: true);
            return;
        }

        RefreshCart(product.Id);
        SaveDraft();
    }

    private async Task OfferDraftRecoveryAsync()
    {
        if (Cart.Lines.Count > 0)
        {
            return;
        }

        var (completed, result) = await _runner.RunAsync(
            "RecuperarBorradorDeVenta",
            () => _useCases.RunAsync<GetSaleDraftHandler, Result<RecoveredDraft?>>(h => h.HandleAsync(CancellationToken.None)));
        if (!completed || result is not { IsSuccess: true } || result.Value is not { } draft)
        {
            return;
        }

        Cart? restored;
        try
        {
            restored = Cart.Restore(draft.DraftId, draft.Lines.Select(l => new CartLine(
                l.ProductId,
                l.Name,
                l.Sku,
                l.UnitCode,
                l.DecimalPlaces,
                l.TracksInventory,
                Money.FromCents(l.CurrentPriceCents),
                Quantity.FromThousandths(l.QuantityThousandths),
                ToUnavailable(l.NotSellableReason))));
        }
        catch (DomainException ex)
        {
            _logger.Warning(ex, "No se pudo restaurar el borrador de la venta. DraftId={DraftId}", draft.DraftId);
            restored = null;
        }

        if (restored is null)
        {
            await DiscardDraftAsync();
            return;
        }

        var recover = await _dialogs.AskAsync(
            Strings.Sale_RecoverTitle,
            string.Format(Display, Strings.Sale_RecoverQuestion, restored.Lines.Count, MoneyConverter.Format(restored.Total.Cents)),
            Strings.Sale_Recover,
            Strings.Sale_Discard);
        if (recover)
        {
            Cart = restored;
            RefreshCart();
        }
        else
        {
            await DiscardDraftAsync();
        }
    }

    private async Task DiscardDraftAsync() =>
        await _runner.RunAsync(
            "DescartarBorradorDeVenta",
            () => _useCases.RunAsync<DiscardSaleDraftHandler, Result>(h => h.HandleAsync(CancellationToken.None)));

    private Task<Result> SaveDraftAsync(Guid draftId, IReadOnlyList<DraftLineDto> lines) =>
        _useCases.RunAsync<SaveSaleDraftHandler, Result>(
            h => h.HandleAsync(new SaveSaleDraftCommand(draftId, lines), CancellationToken.None));

    /// <summary>Guarda el borrador tras cada cambio relevante, sin retraso (FR-010, SC-004).</summary>
    private void SaveDraft() =>
        _autosaver.Save(
            Cart.DraftId,
            [.. Cart.Lines.Select(l => new DraftLineDto(l.ProductId, l.Quantity.Thousandths, l.UnitPrice.Cents))]);

    /// <summary>Vuelve a presentar el <see cref="Cart"/>; con <paramref name="select"/>, deja esa línea seleccionada.</summary>
    private void RefreshCart(Guid? select = null)
    {
        var keep = select ?? SelectedLine?.ProductId;
        Lines.Clear();
        foreach (var line in Cart.Lines)
        {
            Lines.Add(new CartLineRow(line));
        }

        SelectedLine = Lines.FirstOrDefault(l => l.ProductId == keep) ?? Lines.LastOrDefault();
        HasLines = Lines.Count > 0;
        HasUnavailableLines = Cart.Lines.Any(l => l.IsUnavailable);
        TotalText = MoneyConverter.Format(Cart.Total.Cents);
        ItemsText = string.Format(Display, Strings.Sale_Items, Cart.Lines.Count);
        UpdateCanCheckout();

        // Instantánea para el diagnóstico: la venta sin guardar (borrador y cantidad de líneas).
        _diagnostics?.SetSale(Cart.DraftId, Cart.Lines.Count);
    }

    private void UpdateCanCheckout() => CanCheckout = Cart.CanCheckout && !IsModalOpen && !IsSaleBlocked;

    private bool HasSelectedLine() => SelectedLine is not null;

    private void ShowStatus(string message, bool warning)
    {
        StatusMessage = message;
        StatusIsWarning = warning;
        var version = ++_statusVersion;
        _ = ClearStatusLaterAsync(version);
    }

    private async Task ClearStatusLaterAsync(int version)
    {
        await Task.Delay(StatusDuration);
        if (version == _statusVersion)
        {
            StatusMessage = null;
        }
    }

    private static UnavailableReason? ToUnavailable(NotSellableReason? reason) => reason switch
    {
        NotSellableReason.Deleted => UnavailableReason.Deleted,
        NotSellableReason.Inactive => UnavailableReason.Inactive,
        _ => null,
    };
}
