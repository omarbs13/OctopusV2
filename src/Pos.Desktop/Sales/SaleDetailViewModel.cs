using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Pos.Application.Abstractions;
using Pos.Application.Licensing;
using Pos.Application.Returns;
using Pos.Application.Printing.PrintTicket;
using Pos.Application.Sales;
using Pos.Application.Sales.GetSale;
using Pos.Desktop.Auth;
using Pos.Desktop.Common;
using Pos.Desktop.Customers;
using Pos.Desktop.Forms;
using Pos.Desktop.Navigation;
using Pos.Desktop.Resources;
using Pos.Desktop.Settings;
using Pos.Domain.Licensing;
using Pos.Domain.Returns;
using Pos.Domain.Sales;
using Pos.Domain.Users;

namespace Pos.Desktop.Sales;

/// <summary>Línea de una venta registrada, con los valores guardados al venderla.</summary>
public sealed record SaleLineRow(SaleLineDto Line)
{
    public string Name => Line.ProductName;

    public string Sku => Line.ProductSku;

    public string QuantityText => QuantityConverter.Format(Line.QuantityThousandths, Line.DecimalPlaces);

    public string PriceText => MoneyConverter.Format(Line.UnitPriceCents);

    /// <summary>Importe final de la línea: original − descuento de línea (015); no incluye la parte del descuento de venta.</summary>
    public string AmountText => MoneyConverter.Format(Line.AmountAfterLineDiscountCents);

    public bool HasLineDiscount => Line.HasLineDiscount;

    public string OriginalText => MoneyConverter.Format(Line.OriginalCents);

    public string LineDiscountText => MoneyConverter.Format(-Line.LineDiscountCents);

    public bool HasReturned => Line.ReturnedThousandths > 0;

    /// <summary>"Devuelto: X de Y" (contracts/ui.md §1).</summary>
    public string ReturnedText => string.Format(
        CultureInfo.GetCultureInfo("es-MX"),
        Strings.Return_Returned,
        QuantityConverter.Format(Line.ReturnedThousandths, Line.DecimalPlaces),
        QuantityConverter.Format(Line.QuantityThousandths, Line.DecimalPlaces));
}

/// <summary>Cancelación o devolución en el historial de una venta (FR-013); solo lectura.</summary>
public sealed record ReturnHistoryRow(ReturnSummaryDto Item)
{
    public string Text => string.Format(
        CultureInfo.GetCultureInfo("es-MX"),
        Strings.Return_HistoryItem,
        Item.Folio,
        Item.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
        Item.CreatedByName,
        Item.AuthorizedByName,
        MoneyConverter.Format(Item.TotalCents),
        Item.Kind == ReturnKind.Cancellation ? CompensationText(Strings.Return_KindCancellation) : CompensationText(Strings.Return_KindPartial),
        Item.Reason);

    private string CompensationText(string kind) =>
        $"{kind} ({(Item.CreditNoteFolio is { } note ? string.Format(CultureInfo.GetCultureInfo("es-MX"), Strings.Return_CompensationCreditNote, note) : Strings.Return_CompensationRefund)})";
}

/// <summary>Pago de una venta registrada.</summary>
public sealed record SalePaymentRow(SalePaymentDto Payment)
{
    public string MethodText => PaymentMethodLabels.Of(Payment.Method);

    public string AmountText => MoneyConverter.Format(Payment.AmountCents);

    public string ReceivedText => Payment.ReceivedCents is { } received ? MoneyConverter.Format(received) : QuantityConverter.NoValue;

    public string ChangeText => Payment.ChangeCents is { } change ? MoneyConverter.Format(change) : QuantityConverter.NoValue;

    public string ReferenceText => Payment.Reference ?? string.Empty;
}

/// <summary>
/// Detalle de una venta: encabezado, líneas y pagos con los valores guardados, y la cancelación
/// cuando la venta está completada. Es de solo lectura: nunca tiene cambios sin guardar.
/// </summary>
public sealed partial class SaleDetailViewModel : FormViewModel
{
    private static readonly CultureInfo Display = CultureInfo.GetCultureInfo("es-MX");

    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly TicketPrintingService _printing;
    private readonly AdminAuthorizationService? _authorization;
    private readonly ICurrentPermissions? _permissions;
    private readonly ILicenseState? _license;
    private readonly Navigator? _navigator;

    private Guid _saleId;

    public SaleDetailViewModel(
        UseCases useCases,
        OperationRunner runner,
        IDialogService dialogs,
        TicketPrintingService printing,
        AdminAuthorizationService? authorization = null,
        ICurrentPermissions? permissions = null,
        ILicenseState? license = null,
        Navigator? navigator = null)
        : base(dialogs)
    {
        _navigator = navigator;
        _authorization = authorization;
        _permissions = permissions;
        _license = license;
        _printing = printing;
        _useCases = useCases;
        _runner = runner;
    }

    public override string Title => Detail is null
        ? Strings.SaleDetail_Title
        : string.Format(Display, Strings.SaleDetail_TitleWithFolio, Detail.Folio);

    public ObservableCollection<SaleLineRow> Lines { get; } = [];

    public ObservableCollection<SalePaymentRow> Payments { get; } = [];

    /// <summary>Descuentos de la venta (015, FR-017).</summary>
    public ObservableCollection<SaleDiscountRow> Discounts { get; } = [];

    [ObservableProperty]
    public partial bool HasDiscounts { get; private set; }

    [ObservableProperty]
    public partial string DiscountSummaryText { get; private set; } = string.Empty;

    public ObservableCollection<ReturnHistoryRow> History { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(Title),
        nameof(HeaderText),
        nameof(TotalText),
        nameof(IsCancelled),
        nameof(CanCancel),
        nameof(CanReturnItems),
        nameof(IsOutOfReturnWindow),
        nameof(HasHistory),
        nameof(ReturnedTotalText),
        nameof(CancellationText),
        nameof(HasCredit),
        nameof(CreditCustomerText),
        nameof(CreditBalanceText),
        nameof(CreditStatusText))]
    public partial SaleDetailDto? Detail { get; private set; }

    /// <summary>Venta a crédito (014): bloque "Crédito" con cliente, saldo de esta venta y estado.</summary>
    public bool HasCredit => Detail?.Credit is not null;

    public string CreditCustomerText => Detail?.Credit is { } credit ? string.Format(Display, Strings.Credit_SaleCustomer, credit.CustomerName) : string.Empty;

    public string CreditBalanceText => Detail?.Credit is { } credit ? string.Format(Display, Strings.Credit_SaleBalance, MoneyConverter.Format(credit.BalanceCents)) : string.Empty;

    public string CreditStatusText => Detail?.Credit is { } credit ? CreditStatusLabels.Of(credit.Status) : string.Empty;

    /// <summary>Enlace a la ficha del cliente; se oculta cuando el detalle ya se abrió desde la ficha.</summary>
    [ObservableProperty]
    public partial bool ShowCustomerLink { get; set; } = true;

    /// <summary>Formulario de devolución o cancelación, mientras esté abierto.</summary>
    [ObservableProperty]
    public partial ReturnSaleViewModel? CancelForm { get; private set; }

    /// <summary>Permiso y módulo de devoluciones: sin ellos solo queda la cancelación básica de siempre.</summary>
    private bool ReturnsActive =>
        _license?.IsModuleActive(LicensedModule.Returns) != false
        && (_permissions is null || _permissions.Has(Permission.ProcessReturns));

    private bool ModuleActive => _license?.IsModuleActive(LicensedModule.Returns) != false;

    public string HeaderText => Detail is null
        ? string.Empty
        : string.Format(
            Display,
            Strings.SaleDetail_Header,
            Detail.Folio,
            Detail.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
            Detail.CreatedByName,
            StatusText(Detail));

    public string TotalText => Detail is null ? string.Empty : MoneyConverter.Format(Detail.TotalCents);

    public bool IsCancelled => Detail?.Status == SaleStatus.Cancelled;

    /// <summary>
    /// "Cancelar venta" aparece con la venta completada. Con Devoluciones activo se oculta si ya hay
    /// devoluciones parciales, la venta está totalmente devuelta o fuera del plazo (contracts/ui.md §1).
    /// </summary>
    public bool CanCancel =>
        Detail is { Status: SaleStatus.Completed } d
        && CancelForm is null
        && (!ModuleActive || (ReturnsActive && d.ReturnedCents == 0 && d.WithinReturnWindow));

    /// <summary>"Devolver artículos": con permiso y módulo, hasta que la venta esté totalmente devuelta o venza el plazo.</summary>
    public bool CanReturnItems =>
        Detail is { Status: SaleStatus.Completed } d
        && CancelForm is null
        && ReturnsActive
        && !d.IsFullyReturned
        && d.WithinReturnWindow;

    public bool IsOutOfReturnWindow => Detail is { Status: SaleStatus.Completed, WithinReturnWindow: false } && ReturnsActive;

    public bool HasHistory => History.Count > 0;

    public string ReturnedTotalText => Detail is { ReturnedCents: > 0 } d
        ? string.Format(Display, Strings.Return_ReturnedTotal, MoneyConverter.Format(d.ReturnedCents))
        : string.Empty;

    private static string StatusText(SaleDetailDto detail) => detail switch
    {
        { Status: SaleStatus.Cancelled } => Strings.Sales_StatusCancelled,
        { IsFullyReturned: true } => Strings.Return_StatusFull,
        { IsPartiallyReturned: true } => Strings.Return_StatusPartial,
        _ => Strings.Sales_StatusCompleted,
    };

    public string CancellationText => Detail is { Status: SaleStatus.Cancelled } d
        ? string.Format(
            Display,
            Strings.SaleDetail_CancelledInfo,
            d.CancellationReason,
            d.CancelledAtUtc?.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
            d.CancelledByName)
        : string.Empty;

    /// <summary>Carga la venta; falso si no existe o falló (el operador ya vio el mensaje).</summary>
    public async Task<bool> LoadAsync(Guid saleId)
    {
        _saleId = saleId;
        var (completed, result) = await _runner.RunAsync(
            "ConsultarVenta",
            () => _useCases.RunAsync<GetSaleHandler, Result<SaleDetailDto>>(
                h => h.HandleAsync(new GetSaleQuery(saleId), CancellationToken.None)),
            new Dictionary<string, object?> { ["SaleId"] = saleId });

        if (!completed || result is null)
        {
            return false;
        }

        if (!result.IsSuccess)
        {
            await Dialogs.ShowMessageAsync(Strings.Common_InfoTitle, Strings.Editor_NotFound);
            return false;
        }

        Apply(result.Value);
        return true;
    }

    public void StartCancel() => OpenForm(ReturnFormMode.Cancel);

    public void StartReturn() => OpenForm(ReturnFormMode.Items);

    private void OpenForm(ReturnFormMode mode)
    {
        if (Detail is not { Status: SaleStatus.Completed } detail || CancelForm is not null)
        {
            return;
        }

        CancelForm = new ReturnSaleViewModel(
            _useCases,
            _runner,
            Dialogs,
            detail,
            mode,
            OnCancelFinishedAsync,
            CloseCancelForm,
            _authorization,
            _printing,
            isBasic: !ModuleActive);
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(CanReturnItems));
    }

    protected override object CaptureState() => 0;

    protected override Task<bool> SaveCoreAsync() => Task.FromResult(true);

    [CommunityToolkit.Mvvm.Input.RelayCommand(CanExecute = nameof(CanCancel))]
    private void CancelSale() => StartCancel();

    [CommunityToolkit.Mvvm.Input.RelayCommand(CanExecute = nameof(CanReturnItems))]
    private void ReturnItems() => StartReturn();

    /// <summary>Reimprime el ticket (con la leyenda REIMPRESIÓN) sin afectar la venta; vale para completadas y canceladas.</summary>
    [CommunityToolkit.Mvvm.Input.RelayCommand(CanExecute = nameof(CanReprint))]
    private async Task ReprintAsync()
    {
        if (Detail is not { } detail)
        {
            return;
        }

        await _printing.PrintAsync(PrintSource.Sale(detail.Id), isReprint: true, detail.Folio, automatic: false);
    }

    private bool CanReprint() => Detail is not null;

    /// <summary>Lleva a la ficha del cliente de la venta a crédito.</summary>
    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private async Task OpenCustomerAsync()
    {
        if (Detail?.Credit is { } credit && _navigator is not null)
        {
            await _navigator.NavigateAsync(CustomersModule.ListPageId, credit.CustomerId);
        }
    }

    private void CloseCancelForm()
    {
        CancelForm = null;
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(CanReturnItems));
        CancelSaleCommand.NotifyCanExecuteChanged();
        ReturnItemsCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Tras cancelar, o si la venta cambió, vuelve a leerla para mostrar su estado actual.</summary>
    private async Task OnCancelFinishedAsync()
    {
        CloseCancelForm();
        await LoadAsync(_saleId);
    }

    private void Apply(SaleDetailDto detail)
    {
        Lines.Clear();
        foreach (var line in detail.Lines)
        {
            Lines.Add(new SaleLineRow(line));
        }

        Payments.Clear();
        foreach (var payment in detail.Payments)
        {
            Payments.Add(new SalePaymentRow(payment));
        }

        Discounts.Clear();
        foreach (var discount in detail.DiscountList)
        {
            Discounts.Add(new SaleDiscountRow(discount));
        }

        HasDiscounts = detail.HasDiscounts;
        DiscountSummaryText = detail.HasDiscounts
            ? $"{Strings.SaleDetail_Subtotal}: {MoneyConverter.Format(detail.SubtotalCents)} · {Strings.SaleDetail_TotalSaved}: {MoneyConverter.Format(detail.DiscountCents)}"
            : string.Empty;

        History.Clear();
        foreach (var item in detail.ReturnHistory)
        {
            History.Add(new ReturnHistoryRow(item));
        }

        Detail = detail;
        CancelSaleCommand.NotifyCanExecuteChanged();
        ReturnItemsCommand.NotifyCanExecuteChanged();
        ReprintCommand.NotifyCanExecuteChanged();
    }
}

/// <summary>Descuento de una venta registrada con tipo, valor, monto, aplicador y autorizador (015, FR-017).</summary>
public sealed record SaleDiscountRow(SaleDiscountDto Discount)
{
    public string KindText => Pos.Application.Discounts.DiscountMessages.KindText(Discount.Kind);

    /// <summary>Producto de la línea o código del cupón.</summary>
    public string DetailText => Discount.ProductName ?? Discount.CouponCode ?? string.Empty;

    public string ValueText => Discount.Mode == Pos.Domain.Discounts.DiscountMode.Percent
        ? Discount.Discount.ToString()
        : MoneyConverter.Format(Discount.Value);

    public string AmountText => MoneyConverter.Format(-Discount.AmountCents);

    public string AppliedBy => Discount.AppliedByName;

    public string AuthorizedBy => Discount.AuthorizedByName ?? string.Empty;
}
