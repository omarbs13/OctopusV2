using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.CreditNotes;
using Pos.Application.CreditNotes.GetCreditNoteBalance;
using Pos.Application.Customers;
using Pos.Application.Customers.GetCustomerCreditStatus;
using Pos.Application.Discounts;
using Pos.Application.Discounts.ApproveDiscount;
using Pos.Application.Discounts.ResolveCoupon;
using Pos.Application.Discounts.Settings.GetDiscountSettings;
using Pos.Application.Licensing;
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
using Pos.Desktop.Shell;
using Pos.Domain.CashShifts;
using Pos.Domain.Licensing;
using Pos.Domain.Common;
using Pos.Domain.Discounts;
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

    /// <summary>Importe final de la línea: después de su descuento (015).</summary>
    public string AmountText => MoneyConverter.Format(Line.NetBeforeOrder.Cents);

    /// <summary>Importe original, tachado cuando la línea tiene descuento (FR-008).</summary>
    public string OriginalAmountText => MoneyConverter.Format(Line.Amount.Cents);

    public bool HasDiscount => Line.HasDiscount;

    /// <summary>"-10%" o "-$15.00".</summary>
    public string DiscountLabel => Line.Discount is { } discount
        ? "-" + (discount.Value.IsPercent ? discount.Value.ToString() : MoneyConverter.Format(discount.Value.Raw))
        : string.Empty;

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
    private readonly ILicenseState? _license;
    private readonly ModalHost? _sessionModal;

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
        DiagnosticContext? diagnostics = null,
        ILicenseState? license = null,
        ModalHost? sessionModal = null)
    {
        _sessionModal = sessionModal;
        _license = license;
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

    /// <summary>Buscador de clientes para la venta a crédito, mientras esté abierto (014).</summary>
    [ObservableProperty]
    public partial CustomerPickerViewModel? CustomerPicker { get; private set; }

    /// <summary>Diálogo de descuento de línea o de venta, mientras esté abierto (015).</summary>
    [ObservableProperty]
    public partial DiscountDialogViewModel? DiscountDialog { get; private set; }

    /// <summary>Captura de "Aplicar cupón", mientras esté abierta (015).</summary>
    [ObservableProperty]
    public partial CouponEntryViewModel? CouponEntry { get; private set; }

    [ObservableProperty]
    public partial string SubtotalText { get; private set; } = string.Empty;

    /// <summary>"Descuento (10%)" o "Cupón VERANO10".</summary>
    [ObservableProperty]
    public partial string OrderDiscountLabel { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string OrderDiscountText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOrderDiscount { get; private set; }

    /// <summary>Con algún descuento el pie muestra Subtotal, el descuento de venta y Total (FR-008).</summary>
    [ObservableProperty]
    public partial bool HasDiscounts { get; private set; }

    /// <summary>Descuentos y cupones con <c>ApplyDiscounts</c> y el módulo Descuentos y promociones activo (FR-022).</summary>
    public bool CanUseDiscounts =>
        (_permissions?.Has(Permission.ApplyDiscounts) ?? false) && _license?.IsModuleActive(LicensedModule.Discounts) != false;

    /// <summary>El Administrador aprueba sus propios descuentos sin capturar contraseña (FR-005).</summary>
    private bool CanApproveDiscounts => _permissions?.Has(Permission.ApproveDiscounts) ?? false;

    /// <summary>
    /// Cliente elegido para vender a crédito. No se guarda en el borrador (<c>SaleDrafts</c> no cambia):
    /// si la venta se recupera tras un cierre inesperado, el cliente se vuelve a elegir (contracts/ui.md).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCustomer), nameof(CustomerText))]
    public partial CustomerForSaleDto? SelectedCustomer { get; private set; }

    public bool HasCustomer => SelectedCustomer is not null;

    public string CustomerText => SelectedCustomer is { } customer ? string.Format(Display, Strings.Credit_CustomerLabel, customer.Name) : string.Empty;

    /// <summary>"Cliente…" aparece con <c>SellOnCredit</c> y el módulo Crédito y clientes activo.</summary>
    public bool CanUseCredit =>
        (_permissions?.Has(Permission.SellOnCredit) ?? false) && _license?.IsModuleActive(LicensedModule.CreditAndCustomers) != false;

    [ObservableProperty]
    public partial bool IsEditingQuantity { get; private set; }

    [ObservableProperty]
    public partial string QuantityEditText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? QuantityEditError { get; private set; }

    /// <summary>Hay una ventana modal sobre la venta (selector o cobro): los atajos de captura no aplican.</summary>
    public bool IsModalOpen => Chooser is not null || Checkout is not null || DrawerReason is not null || CustomerPicker is not null
        || DiscountDialog is not null || CouponEntry is not null;

    /// <summary>
    /// Un diálogo propio de la venta o uno de la sesión (autorización del Administrador) está abierto: las
    /// lecturas del escáner se ignoran (021, FR-008).
    /// </summary>
    public bool IsDialogOpen => IsModalOpen || _sessionModal?.IsOpen == true;

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
        OnPropertyChanged(nameof(CanUseDiscounts));
        await RefreshShiftAsync();
        if (_license?.Current.IsBlocked == true)
        {
            // 025, FR-030a: la venta en curso se termina y se cobra; no se inician ventas nuevas.
            ShowStatus(Strings.License_SaleInProgressBlocked, warning: true);
        }

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

    partial void OnCustomerPickerChanged(CustomerPickerViewModel? value) => OnModalChanged();

    partial void OnDiscountDialogChanged(DiscountDialogViewModel? value) => OnModalChanged();

    partial void OnCouponEntryChanged(CouponEntryViewModel? value) => OnModalChanged();

    /// <summary>"Cliente…": abre el buscador de clientes con crédito.</summary>
    [RelayCommand]
    private async Task ChooseCustomerAsync()
    {
        if (!CanUseCredit || IsModalOpen || IsSaleBlocked)
        {
            return;
        }

        var picker = new CustomerPickerViewModel(
            _useCases,
            _runner,
            chosen =>
            {
                CloseCustomerPicker();
                SelectedCustomer = chosen;
            },
            CloseCustomerPicker);
        CustomerPicker = picker;
        await picker.LoadAsync();
    }

    /// <summary>"×": quita el cliente; la venta sigue y se cobra de contado.</summary>
    [RelayCommand]
    private void ClearCustomer() => SelectedCustomer = null;

    private void CloseCustomerPicker()
    {
        CustomerPicker?.Dispose();
        CustomerPicker = null;
    }

    /// <summary>Estado de crédito del cliente para el cobro; el error ya viene en español.</summary>
    private async Task<(CustomerCreditStatusDto? Status, string? Error)> LoadCreditStatusAsync(Guid customerId, long totalCents)
    {
        var query = new GetCustomerCreditStatusQuery(customerId, totalCents);
        var (completed, result) = await _runner.RunAsync(
            "ConsultarCreditoDelCliente",
            () => _useCases.RunAsync<GetCustomerCreditStatusHandler, Result<CustomerCreditStatusDto>>(h => h.HandleAsync(query, CancellationToken.None)),
            new Dictionary<string, object?> { ["CustomerId"] = customerId });
        if (!completed || result is null)
        {
            return (null, Strings.Common_UnexpectedError);
        }

        return result.Error switch
        {
            null => (result.Value, null),
            CustomerNotEligibleForCredit or NotFound => (null, Strings.Credit_NotEligible),
            ModuleNotLicensed => (null, Strings.License_ModuleNotLicensed),
            Forbidden => (null, Strings.Common_Forbidden),
            _ => (null, Strings.Common_UnexpectedError),
        };
    }

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
    private void Capture() => Capture(isScan: false);

    /// <summary>
    /// Enter en el campo de captura desde la vista; <paramref name="isScan"/> indica que el texto llegó como
    /// ráfaga del escáner (021): entonces no se busca por nombre.
    /// </summary>
    public void Capture(bool isScan)
    {
        var text = CaptureText.Trim();
        CaptureText = string.Empty;
        if (text.Length == 0)
        {
            DismissLastSale();
            return;
        }

        _scans.Enqueue(new ScanInput(text, isScan));
    }

    /// <summary>Asigna el guardián de lecturas a los diálogos en ventana propia mientras la venta está en pantalla (021, FR-008).</summary>
    public void AttachScanGuard() => _dialogs.ScanIgnored = ReportScanIgnoredInDialog;

    public void DetachScanGuard()
    {
        if (_dialogs.ScanIgnored == ReportScanIgnoredInDialog)
        {
            _dialogs.ScanIgnored = null;
        }
    }

    /// <summary>Una ráfaga del escáner llegó con un diálogo abierto: se ignoró y se avisa (021, FR-008).</summary>
    public void ReportScanIgnoredInDialog()
    {
        ShowStatus(Strings.Scan_IgnoredInDialog, warning: true);
        _logger.Information("Lectura ignorada por diálogo abierto");
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
        NotifyOrderDiscountRemoved();
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
        NotifyOrderDiscountRemoved();
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
        NotifyOrderDiscountRemoved();
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
        // Los pagos capturados se conservan mientras no cambien el total ni el cliente (US3, escenario 7).
        if (_pendingCheckout is null || _pendingCheckout.Total != Cart.Total || _pendingCheckout.ChosenCustomerId != SelectedCustomer?.Id)
        {
            // La nota de crédito se ofrece solo con el módulo Devoluciones activo (013) y la venta a
            // crédito solo con un cliente elegido (014).
            var customer = CanUseCredit ? SelectedCustomer : null;
            _pendingCheckout = new CheckoutViewModel(
                new Domain.Sales.Checkout(Cart.Total),
                ConfirmSaleAsync,
                CloseCheckout,
                _license?.IsModuleActive(LicensedModule.Returns) != false ? LookupCreditNoteAsync : null,
                customer is null ? null : new CreditCheckoutOption(customer, total => LoadCreditStatusAsync(customer.Id, total)));
        }

        Checkout = _pendingCheckout;
        _pendingCheckout.RequestFocus();
    }

    private void CloseCheckout() => Checkout = null;

    /// <summary>Saldo de una nota de crédito por folio para el cobro; el error ya viene en español.</summary>
    private async Task<(CreditNoteBalance? Balance, string? Error)> LookupCreditNoteAsync(string folio)
    {
        var (completed, result) = await _runner.RunAsync(
            "ConsultarNotaDeCredito",
            () => _useCases.RunAsync<GetCreditNoteBalanceHandler, Result<CreditNoteBalance>>(h => h.HandleAsync(folio, CancellationToken.None)),
            new Dictionary<string, object?>());
        if (!completed || result is null)
        {
            return (null, Strings.Common_UnexpectedError);
        }

        return result.Error switch
        {
            null => (result.Value, null),
            CreditNoteNotFound => (null, Strings.CreditNote_NotFound),
            ValidationFailed validation => (null, validation.Errors is [{ } first, ..] ? first.Message : Strings.CreditNote_NotFound),
            ModuleNotLicensed => (null, Strings.License_ModuleNotLicensed),
            _ => (null, Strings.Common_UnexpectedError),
        };
    }

    private Task ConfirmSaleAsync(CheckoutViewModel checkout) => ConfirmSaleAsync(checkout, allowAuthorization: true);

    private async Task ConfirmSaleAsync(CheckoutViewModel checkout, bool allowAuthorization)
    {
        await _autosaver.FlushAsync();

        var command = new ConfirmSaleCommand(
            Cart.DraftId,
            [.. Cart.Lines.Select(l => new ConfirmLineInput(
                l.ProductId,
                l.Quantity.Thousandths,
                l.UnitPrice.Cents,
                l.Discount is { } d ? new LineDiscountInput(d.Value.Mode, d.Value.Raw, d.ApprovalId) : null))],
            [.. checkout.ToPayments().Select(p => new PaymentInput(p.Method, p.Amount.Cents, p.Received?.Cents, p.Reference))],
            checkout.CustomerId,
            checkout.OverLimitGrantId,
            Cart.OrderDiscount switch
            {
                OrderDiscount.Manual m => OrderDiscountInput.Manual(m.Value.Mode, m.Value.Raw, m.ApprovalId),
                OrderDiscount.CouponApplied c => OrderDiscountInput.Coupon(c.Code),
                _ => null,
            });
        checkout.OverLimitGrantId = null;
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

            case ModuleNotLicensed:
                checkout.ErrorMessage = Strings.License_ModuleNotLicensed;
                break;

            case SystemNotActivated:
                // 025: en bloqueo solo se cobra la venta en curso; una venta nueva se rechaza.
                checkout.ErrorMessage = Strings.License_SystemNotActivated;
                break;

            case CreditNoteNotFound:
                checkout.ErrorMessage = Strings.CreditNote_NotFound;
                break;

            case InsufficientCreditNote insufficient:
                checkout.ErrorMessage = string.Format(Display, Strings.CreditNote_Insufficient, MoneyConverter.Format(insufficient.AvailableCents));
                break;

            case CreditLimitExceeded exceeded:
                // 014: se pide la autorización de un Administrador y se reintenta una vez con la concesión;
                // cancelar o una contraseña incorrecta no registran nada (Historia 2, escenario 3).
                checkout.ErrorMessage = string.Format(Display, Strings.Credit_LimitExceeded, MoneyConverter.Format(exceeded.ExcessCents));
                if (allowAuthorization && _authorization is not null
                    && await _authorization.RequestAsync(Permission.ApproveCreditOverLimit) is { } grant)
                {
                    checkout.OverLimitGrantId = grant;
                    await ConfirmSaleAsync(checkout, allowAuthorization: false);
                }

                break;

            case CustomerNotEligibleForCredit:
                checkout.ErrorMessage = Strings.Credit_NotEligible;
                break;

            case DiscountApprovalRequired required:
                // 015: el límite bajó o el descuento cambió; se señala, se pide autorización y se reintenta una vez.
                await ReauthorizeAndRetryAsync(checkout, required, allowAuthorization);
                break;

            case CouponNotValid invalid:
                // 015, FR-012: se retira el cupón, se recalcula y no se cobra hasta que el cajero confirme de nuevo.
                RemoveOrderDiscountBeforeRetry(invalid.Status is { } status
                    ? DiscountMessages.CouponRejected(invalid.Code, status, invalid.StartsOn, invalid.EndsOn) ?? Strings.Sale_NotRegistered
                    : DiscountMessages.CouponNotFound(invalid.Code));
                break;

            case OrderDiscountRemoved removed:
                RemoveOrderDiscountBeforeRetry(DiscountMessages.OrderDiscountRemoved(MoneyConverter.Format(removed.DiscountCents)));
                break;

            default:
                checkout.ErrorMessage = Strings.Sale_NotRegistered;
                break;
        }
    }

    /// <summary>F7: descuento de la línea seleccionada en % o $ (015, Historia 1).</summary>
    [RelayCommand]
    private void OpenLineDiscount()
    {
        if (!CanUseDiscounts || IsModalOpen || IsSaleBlocked)
        {
            return;
        }

        if (SelectedLine is not { } row)
        {
            ShowStatus(Strings.Discount_SelectLine, warning: true);
            return;
        }

        var line = row.Line;
        DiscountDialog = new DiscountDialogViewModel(
            Strings.Discount_LineTitle,
            line.Amount.Cents,
            line.Discount?.Value,
            value => ApplyLineDiscountAsync(line.ProductId, value),
            line.Discount is null ? null : () => RemoveLineDiscount(line.ProductId),
            CloseDiscountDialog);
    }

    /// <summary>Shift+F7: descuento de la venta sobre el subtotal (015, Historia 2).</summary>
    [RelayCommand]
    private void OpenOrderDiscount()
    {
        if (!CanUseDiscounts || IsModalOpen || IsSaleBlocked || Cart.Lines.Count == 0)
        {
            return;
        }

        DiscountDialog = new DiscountDialogViewModel(
            Strings.Discount_OrderTitle,
            Cart.Subtotal.Cents,
            (Cart.OrderDiscount as OrderDiscount.Manual)?.Value,
            ApplyOrderDiscountAsync,
            Cart.OrderDiscount is null ? null : () =>
            {
                CloseDiscountDialog();
                RemoveOrderDiscount();
            },
            CloseDiscountDialog);
    }

    /// <summary>"Aplicar cupón": captura el código y sigue el mismo flujo que el campo de productos (FR-011).</summary>
    [RelayCommand]
    private void OpenCouponEntry()
    {
        if (!CanUseDiscounts || IsModalOpen || IsSaleBlocked || Cart.Lines.Count == 0)
        {
            return;
        }

        CouponEntry = new CouponEntryViewModel(
            async code =>
            {
                var error = await ResolveAndApplyCouponAsync(code);
                if (error is null)
                {
                    CouponEntry = null;
                }

                return error;
            },
            () => CouponEntry = null);
    }

    /// <summary>Botón del pie: quita el descuento de la venta o el cupón sin pedir autorización (FR-007).</summary>
    [RelayCommand]
    private void RemoveOrderDiscount()
    {
        if (Cart.OrderDiscount is null)
        {
            return;
        }

        Cart.SetOrderDiscount(null);
        RefreshCart();
        SaveDraft();
    }

    private void CloseDiscountDialog() => DiscountDialog = null;

    private void RemoveLineDiscount(Guid productId)
    {
        CloseDiscountDialog();
        Cart.SetLineDiscount(productId, null);
        RefreshCart(productId);
        SaveDraft();
    }

    /// <summary>Aplica el descuento a la línea, con autorización si supera el límite; devuelve el error o nulo.</summary>
    private async Task<string?> ApplyLineDiscountAsync(Guid productId, DiscountValue value)
    {
        if (Cart.Lines.FirstOrDefault(l => l.ProductId == productId) is not { } line)
        {
            return Strings.Discount_SelectLine;
        }

        var approval = await AuthorizeIfNeededAsync(DiscountScope.Line, productId, value, line.Amount.Cents, line.Name);
        if (!approval.Proceed)
        {
            return approval.Error;
        }

        try
        {
            Cart.SetLineDiscount(productId, new LineDiscount(value, approval.ApprovalId));
        }
        catch (DomainException ex)
        {
            return ex.Message;
        }

        CloseDiscountDialog();
        RefreshCart(productId);
        NotifyOrderDiscountRemoved();
        SaveDraft();
        return null;
    }

    /// <summary>Aplica el descuento de venta; si ya hay un cupón pregunta si se reemplaza (FR-013).</summary>
    private async Task<string?> ApplyOrderDiscountAsync(DiscountValue value)
    {
        if (Cart.OrderDiscount is OrderDiscount.CouponApplied coupon
            && !await _dialogs.ConfirmAsync(
                Strings.Discount_ReplaceTitle,
                string.Format(Display, Strings.Discount_ReplaceCoupon, coupon.Code),
                Strings.Discount_Replace))
        {
            CloseDiscountDialog();
            return null;
        }

        var approval = await AuthorizeIfNeededAsync(DiscountScope.Order, null, value, Cart.Subtotal.Cents, null);
        if (!approval.Proceed)
        {
            return approval.Error;
        }

        try
        {
            Cart.SetOrderDiscount(new OrderDiscount.Manual(value, approval.ApprovalId));
        }
        catch (DomainException ex)
        {
            return ex.Message;
        }

        CloseDiscountDialog();
        RefreshCart();
        SaveDraft();
        return null;
    }

    /// <summary>Busca el código con <c>ResolveCoupon</c> y lo aplica; devuelve la causa si no se aplicó.</summary>
    private async Task<string?> ResolveAndApplyCouponAsync(string code)
    {
        var query = new ResolveCouponQuery(code);
        var (completed, result) = await _runner.RunAsync(
            "BuscarCupon",
            () => _useCases.RunAsync<ResolveCouponHandler, Result<CouponLookupDto?>>(h => h.HandleAsync(query, CancellationToken.None)));
        if (!completed || result is null)
        {
            return Strings.Common_UnexpectedError;
        }

        return result.Error switch
        {
            null when result.Value is { } coupon => await ApplyCouponAsync(coupon),
            null => DiscountMessages.CouponNotFound(Coupon.NormalizeCode(code)),
            ValidationFailed validation => validation.Errors is [{ } first, ..] ? first.Message : Strings.Common_UnexpectedError,
            ModuleNotLicensed => Strings.License_ModuleNotLicensed,
            Forbidden => Strings.Common_Forbidden,
            _ => Strings.Common_UnexpectedError,
        };
    }

    /// <summary>
    /// Aplica un cupón encontrado: sin autorización ni límite (FR-013). Rechaza uno no vigente o un segundo
    /// cupón con la causa; con un descuento manual pregunta si se reemplaza. Vacío = el cajero no confirmó.
    /// </summary>
    private async Task<string?> ApplyCouponAsync(CouponLookupDto coupon)
    {
        if (coupon.Status != CouponStatus.Active)
        {
            return DiscountMessages.CouponRejected(coupon);
        }

        switch (Cart.OrderDiscount)
        {
            case OrderDiscount.CouponApplied existing:
                return DiscountMessages.CouponAlreadyApplied(existing.Code);
            case OrderDiscount.Manual when !await _dialogs.ConfirmAsync(
                Strings.Discount_ReplaceTitle,
                string.Format(Display, Strings.Discount_ReplaceManual, coupon.Code),
                Strings.Discount_Replace):
                return string.Empty;
        }

        try
        {
            Cart.SetOrderDiscount(new OrderDiscount.CouponApplied(coupon.CouponId, coupon.Code, coupon.Discount));
        }
        catch (DomainException ex)
        {
            return ex.Message;
        }

        RefreshCart();
        SaveDraft();
        ShowStatus(string.Format(Display, Strings.Discount_CouponApplied, coupon.Code), warning: false);
        return null;
    }

    /// <summary>
    /// Si el descuento supera el límite vigente pide la autorización (FR-005): el Administrador aprueba sin
    /// contraseña; un Cajero pasa por el diálogo de 007, cuyo intento queda en la bitácora con el descuento
    /// (FR-019). Guarda la aprobación con <c>ApproveDiscount</c> y devuelve su id.
    /// </summary>
    private async Task<(bool Proceed, Guid? ApprovalId, string? Error)> AuthorizeIfNeededAsync(
        DiscountScope scope,
        Guid? productId,
        DiscountValue value,
        long baseCents,
        string? productName)
    {
        long amount;
        try
        {
            amount = DiscountMath.Amount(baseCents, value);
        }
        catch (DomainException ex)
        {
            return (false, null, ex.Message);
        }

        var (loaded, settings) = await _runner.RunAsync(
            "LeerLimiteDeDescuento",
            () => _useCases.RunAsync<GetDiscountSettingsHandler, Result<DiscountSettings>>(h => h.HandleAsync(CancellationToken.None)));
        if (!loaded || settings is not { IsSuccess: true })
        {
            return (false, null, settings?.Error is ModuleNotLicensed ? Strings.License_ModuleNotLicensed : Strings.Discount_SettingsUnavailable);
        }

        if (!DiscountMath.ExceedsLimit(amount, baseCents, settings.Value.LimitBasisPoints))
        {
            return (true, null, null);
        }

        Guid? grant = null;
        if (!CanApproveDiscounts)
        {
            var description = DiscountTexts.Describe(scope, value, amount, baseCents)
                + (productName is null ? string.Empty : $". Producto {productName}")
                + $". Venta en curso {Cart.DraftId}";
            grant = _authorization is null ? null : await _authorization.RequestAsync(Permission.ApproveDiscounts, description);
            if (grant is null)
            {
                return (false, null, Strings.Discount_NotAuthorized);
            }
        }

        var command = new ApproveDiscountCommand(Cart.DraftId, scope, productId, value.Mode, value.Raw, baseCents, grant);
        var (completed, result) = await _runner.RunAsync(
            "AprobarDescuento",
            () => _useCases.RunAsync<ApproveDiscountHandler, Result<DiscountApprovalDto>>(h => h.HandleAsync(command, CancellationToken.None)),
            new Dictionary<string, object?> { ["DraftId"] = Cart.DraftId, ["Scope"] = scope.ToString() });
        if (!completed || result is null)
        {
            return (false, null, Strings.Common_UnexpectedError);
        }

        return result.Error switch
        {
            null => (true, result.Value.ApprovalId, null),
            ApprovalNotNeeded => (true, null, null),
            Forbidden => (false, null, Strings.Discount_NotAuthorized),
            ModuleNotLicensed => (false, null, Strings.License_ModuleNotLicensed),
            ValidationFailed validation => (false, null, validation.Errors is [{ } first, ..] ? first.Message : Strings.Common_UnexpectedError),
            _ => (false, null, Strings.Common_UnexpectedError),
        };
    }

    /// <summary>
    /// <c>DiscountApprovalRequired</c> al cobrar: señala la línea o la venta, pide la autorización con el
    /// límite vigente y reintenta el cobro una sola vez (contracts/ui.md).
    /// </summary>
    private async Task ReauthorizeAndRetryAsync(CheckoutViewModel checkout, DiscountApprovalRequired required, bool allowAuthorization)
    {
        var line = required.ProductId is { } productId ? Cart.Lines.FirstOrDefault(l => l.ProductId == productId) : null;
        checkout.ErrorMessage = line is not null
            ? string.Format(Display, Strings.Discount_ApprovalRequiredLine, line.Name)
            : Strings.Discount_ApprovalRequiredOrder;
        if (line is not null)
        {
            SelectedLine = Lines.FirstOrDefault(l => l.ProductId == line.ProductId);
        }

        if (!allowAuthorization)
        {
            return;
        }

        (bool Proceed, Guid? ApprovalId, string? Error) approval;
        if (line is { Discount: { } discount })
        {
            approval = await AuthorizeIfNeededAsync(DiscountScope.Line, line.ProductId, discount.Value, line.Amount.Cents, line.Name);
            if (approval.Proceed)
            {
                Cart.SetLineDiscount(line.ProductId, discount with { ApprovalId = approval.ApprovalId });
            }
        }
        else if (Cart.OrderDiscount is OrderDiscount.Manual manual)
        {
            approval = await AuthorizeIfNeededAsync(DiscountScope.Order, null, manual.Value, Cart.Subtotal.Cents, null);
            if (approval.Proceed)
            {
                Cart.SetOrderDiscount(manual with { ApprovalId = approval.ApprovalId });
            }
        }
        else
        {
            return;
        }

        if (!approval.Proceed)
        {
            checkout.ErrorMessage = approval.Error;
            return;
        }

        RefreshCart(line?.ProductId);
        SaveDraft();
        await ConfirmSaleAsync(checkout, allowAuthorization: false);
    }

    /// <summary>Retira el descuento de venta o el cupón, recalcula y cierra el cobro con el aviso (FR-012).</summary>
    private void RemoveOrderDiscountBeforeRetry(string message)
    {
        Cart.SetOrderDiscount(null);
        CloseCheckout();
        _pendingCheckout = null;
        RefreshCart();
        SaveDraft();
        ShowStatus(message, warning: true);
    }

    /// <summary>Aviso cuando el último cambio retiró el descuento global de monto fijo (Historia 2, escenario 5).</summary>
    private void NotifyOrderDiscountRemoved()
    {
        if (Cart.LastRemovedOrderDiscount is { } removed)
        {
            ShowStatus(DiscountMessages.OrderDiscountRemoved(MoneyConverter.Format(removed.Value.Raw)), warning: true);
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
        NotifyOrderDiscountRemoved();
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
        SelectedCustomer = null;
        IsEditingQuantity = false;
        RefreshCart();
        SaveDraft();
    }

    private void DismissLastSale()
    {
        LastSaleText = null;
        LastChangeText = null;
    }

    private async Task ProcessCodeAsync(ScanInput input)
    {
        if (IsSaleBlocked)
        {
            return;
        }

        DismissLastSale();
        var text = input.Text;
        var (completed, result) = await _runner.RunAsync(
            "BuscarProductoParaVender",
            () => _useCases.RunAsync<FindProductsForSaleHandler, Result<ProductLookup>>(
                h => h.HandleAsync(new FindProductsForSaleQuery(text, input.IsScan), CancellationToken.None)),
            new Dictionary<string, object?> { ["Text"] = text, ["IsScan"] = input.IsScan });

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

            case LookupKind.Coupon when lookup.Coupon is { } coupon:
                // 015, FR-011: un código de cupón en el campo de productos aplica el cupón.
                var error = await ApplyCouponAsync(coupon);
                if (!string.IsNullOrEmpty(error))
                {
                    ShowStatus(error, warning: true);
                }

                break;

            default:
                ShowCodeNotFound(text, lookup.Format ?? Barcode.Classify(text), input.IsScan);
                break;
        }
    }

    /// <summary>
    /// Sin coincidencias (021, FR-011, FR-014): "Código no válido" o "Código no encontrado" con F2 para la
    /// búsqueda manual; el aviso se reemplaza con la siguiente lectura o acción.
    /// </summary>
    private void ShowCodeNotFound(string text, BarcodeFormat format, bool isScan)
    {
        if (format == BarcodeFormat.Empty)
        {
            return;
        }

        var normalized = Barcode.Normalize(text);
        _logger.Information(
            "Código sin coincidencias en el Punto de venta. Texto={Text} Formato={Format} Escaneo={IsScan}",
            normalized,
            format,
            isScan);
        var message = format == BarcodeFormat.Unrecognized ? Strings.Scan_CodeInvalid : Strings.Scan_CodeNotFound;
        ShowStatus(string.Format(Display, message, normalized), warning: true);
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
                ToUnavailable(l.NotSellableReason),
                l.Discount is { } d ? new LineDiscount(DiscountValue.Create(d.Mode, d.Value), d.ApprovalId) : null)),
                draft.OrderDiscount is { IsCoupon: false, Mode: { } mode, Value: { } value } manual
                    ? new OrderDiscount.Manual(DiscountValue.Create(mode, value), manual.ApprovalId)
                    : null);
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
            if (draft.OrderDiscount is { IsCoupon: true, Code: { } code })
            {
                // El cupón de la venta conservada se revalida al retomarla (FR-012).
                var error = await ResolveAndApplyCouponAsync(code);
                if (!string.IsNullOrEmpty(error))
                {
                    ShowStatus(error, warning: true);
                }
            }

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

    private Task<Result> SaveDraftAsync(Guid draftId, IReadOnlyList<DraftLineDto> lines, DraftOrderDiscountDto? order) =>
        _useCases.RunAsync<SaveSaleDraftHandler, Result>(
            h => h.HandleAsync(new SaveSaleDraftCommand(draftId, lines, order), CancellationToken.None));

    /// <summary>Guarda el borrador tras cada cambio relevante, sin retraso (FR-010, SC-004), con sus descuentos y aprobaciones (015).</summary>
    private void SaveDraft() =>
        _autosaver.Save(
            Cart.DraftId,
            [.. Cart.Lines.Select(l => new DraftLineDto(
                l.ProductId,
                l.Quantity.Thousandths,
                l.UnitPrice.Cents,
                l.Discount is { } d ? new DraftDiscountDto(d.Value.Mode, d.Value.Raw, d.ApprovalId) : null))],
            Cart.OrderDiscount switch
            {
                OrderDiscount.Manual m => DraftOrderDiscountDto.Manual(m.Value.Mode, m.Value.Raw, m.ApprovalId),
                OrderDiscount.CouponApplied c => DraftOrderDiscountDto.ForCoupon(c.Code),
                _ => null,
            });

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
        SubtotalText = MoneyConverter.Format(Cart.Subtotal.Cents);
        HasDiscounts = Cart.HasDiscounts;
        HasOrderDiscount = Cart.OrderDiscount is not null;
        OrderDiscountText = MoneyConverter.Format(-Cart.OrderDiscountAmount.Cents);
        OrderDiscountLabel = Cart.OrderDiscount switch
        {
            OrderDiscount.CouponApplied c => string.Format(Display, Strings.Discount_CouponLabel, c.Code),
            OrderDiscount.Manual { Value.IsPercent: true } m => string.Format(Display, Strings.Discount_OrderLabel, m.Value),
            OrderDiscount.Manual => Strings.Discount_OrderLabelAmount,
            _ => string.Empty,
        };
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
