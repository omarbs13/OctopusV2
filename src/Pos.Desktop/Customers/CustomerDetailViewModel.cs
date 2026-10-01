using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.CashShifts;
using Pos.Application.CashShifts.GetCurrentShift;
using Pos.Application.Customers;
using Pos.Application.Customers.GetCustomer;
using Pos.Application.Customers.SetCustomerActive;
using Pos.Application.Licensing;
using Pos.Application.Printing.PrintTicket;
using Pos.Application.Receivables;
using Pos.Application.Receivables.ListCustomerPayments;
using Pos.Application.Receivables.ListCustomerReceivables;
using Pos.Desktop.Auth;
using Pos.Desktop.Common;
using Pos.Desktop.Forms;
using Pos.Desktop.Resources;
using Pos.Desktop.Sales;
using Pos.Desktop.Settings;
using Pos.Domain.Licensing;
using Pos.Domain.Receivables;
using Pos.Domain.Users;

namespace Pos.Desktop.Customers;

/// <summary>Venta a crédito en la ficha del cliente, con los textos ya formateados.</summary>
public sealed record CustomerReceivableRow(ReceivableRowDto Item)
{
    public Guid SaleId => Item.SaleId;

    public string Folio => Item.SaleFolio;

    public string DateText => Item.SaleDateUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public string AmountText => MoneyConverter.Format(Item.OriginalCents);

    public string BalanceText => MoneyConverter.Format(Item.BalanceCents);

    public string StatusText => CreditStatusLabels.Of(Item.Status);

    public bool IsOverdue => Item.DaysOverdue > 0;

    public string DaysOverdueText => Item.DaysOverdue > 0 ? Item.DaysOverdue.ToString(MoneyConverter.Culture) : string.Empty;

    public bool IsCancelled => Item.Status == ReceivableStatus.Cancelled;
}

/// <summary>Abono en la ficha del cliente; "Anulado" en rojo con el motivo en la información emergente.</summary>
public sealed record CustomerPaymentItem(CustomerPaymentRowDto Item)
{
    public string Folio => Item.Folio;

    public string DateText => Item.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public string AmountText => MoneyConverter.Format(Item.AmountCents);

    public string MethodText => PaymentMethodLabels.Of(Item.Method);

    public string ReferenceText => Item.Reference ?? string.Empty;

    public string BeforeText => MoneyConverter.Format(Item.BalanceBeforeCents);

    public string AfterText => MoneyConverter.Format(Item.BalanceAfterCents);

    public bool IsVoided => Item.Status == CustomerPaymentStatus.Voided;

    public string StatusText => IsVoided ? Strings.Payment_StatusVoided : Strings.Payment_StatusActive;

    public string? VoidTip => IsVoided ? string.Format(MoneyConverter.Culture, Strings.Payment_VoidedTip, Item.VoidReason) : null;
}

/// <summary>
/// Ficha del cliente (contracts/ui.md "Clientes: ficha"): encabezado con saldo, disponible y días
/// vencido tal como los calcula <c>GetCustomer</c>; editar; activar o desactivar (solo
/// <c>ManageCustomerCredit</c>); y las pestañas "Ventas a crédito" y "Abonos". Es de solo lectura:
/// nunca tiene cambios sin guardar.
/// </summary>
public sealed partial class CustomerDetailViewModel : FormViewModel
{
    private static readonly CultureInfo Display = MoneyConverter.Culture;

    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly Func<CustomerFormViewModel> _formFactory;
    private readonly ICurrentPermissions? _permissions;
    private readonly Func<SaleDetailViewModel>? _saleDetailFactory;
    private readonly TicketPrintingService? _printing;
    private readonly AdminAuthorizationService? _authorization;
    private readonly ILicenseState? _license;

    private Guid _customerId;

    public CustomerDetailViewModel(
        UseCases useCases,
        OperationRunner runner,
        IDialogService dialogs,
        Func<CustomerFormViewModel> formFactory,
        ICurrentPermissions? permissions = null,
        Func<SaleDetailViewModel>? saleDetailFactory = null,
        TicketPrintingService? printing = null,
        AdminAuthorizationService? authorization = null,
        ILicenseState? license = null)
        : base(dialogs)
    {
        _printing = printing;
        _authorization = authorization;
        _license = license;
        _useCases = useCases;
        _runner = runner;
        _formFactory = formFactory;
        _permissions = permissions;
        _saleDetailFactory = saleDetailFactory;
    }

    /// <summary>Pestaña "Ventas a crédito".</summary>
    public ObservableCollection<CustomerReceivableRow> Receivables { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenSaleCommand))]
    public partial CustomerReceivableRow? SelectedReceivable { get; set; }

    [ObservableProperty]
    public partial bool HasNoReceivables { get; private set; }

    /// <summary>Pestaña "Abonos", con los anulados visibles.</summary>
    public ObservableCollection<CustomerPaymentItem> Payments { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReprintReceiptCommand), nameof(VoidPaymentCommand))]
    public partial CustomerPaymentItem? SelectedPayment { get; set; }

    [ObservableProperty]
    public partial bool HasNoPayments { get; private set; }

    /// <summary>"Registrar abono", "Reimprimir recibo" y "Anular" se ven con <c>RegisterCustomerPayments</c>.</summary>
    public bool CanRegisterPayments => _permissions?.Has(Permission.RegisterCustomerPayments) ?? true;

    public override string Title => Detail is null ? Strings.Customer_ListTitle : string.Format(Display, Strings.Customer_DetailTitle, Detail.Name);

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(Title),
        nameof(ContactText),
        nameof(ModeText),
        nameof(LimitText),
        nameof(BalanceText),
        nameof(AvailableText),
        nameof(DaysOverdueText),
        nameof(HasOverdue),
        nameof(IsInactive),
        nameof(CanDeactivate),
        nameof(CanActivate))]
    public partial CustomerDetailDto? Detail { get; private set; }

    /// <summary>Formulario o diálogo abierto dentro de la ficha (editar, registrar o anular abono).</summary>
    [ObservableProperty]
    public partial object? ActiveDialog { get; private set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    public Guid CustomerId => _customerId;

    public string ContactText => Detail is null
        ? string.Empty
        : string.Format(Display, Strings.Customer_Contact, Detail.Phone, Detail.Email ?? "—", Detail.TaxId ?? "—");

    public string ModeText => Detail is null ? string.Empty : CustomerTexts.Mode(Detail.CreditMode);

    public string LimitText => Detail is null ? string.Empty : MoneyConverter.Format(Detail.LimitCents);

    public string BalanceText => Detail is null ? string.Empty : MoneyConverter.Format(Detail.BalanceCents);

    public string AvailableText => Detail is null ? string.Empty : MoneyConverter.Format(Detail.AvailableCents);

    public bool HasOverdue => Detail is { DaysOverdue: > 0 };

    public string DaysOverdueText => Detail is null ? string.Empty : Detail.DaysOverdue.ToString(Display);

    public bool IsInactive => Detail is { IsActive: false };

    private bool CanManageCredit => _permissions?.Has(Permission.ManageCustomerCredit) ?? true;

    public bool CanDeactivate => Detail is { IsActive: true } && CanManageCredit;

    public bool CanActivate => Detail is { IsActive: false } && CanManageCredit;

    /// <summary>Carga la ficha; falso si no existe o falló (el operador ya vio el mensaje).</summary>
    public async Task<bool> LoadAsync(Guid customerId)
    {
        _customerId = customerId;
        var (completed, result) = await _runner.RunAsync(
            "ConsultarCliente",
            () => _useCases.RunAsync<GetCustomerHandler, Result<CustomerDetailDto>>(
                h => h.HandleAsync(new GetCustomerQuery(customerId), CancellationToken.None)),
            new Dictionary<string, object?> { ["CustomerId"] = customerId });

        if (!completed || result is null)
        {
            return false;
        }

        if (!result.IsSuccess)
        {
            await Dialogs.ShowMessageAsync(Strings.Common_InfoTitle, result.Error is Forbidden ? Strings.Common_Forbidden : Strings.Customer_NotFound);
            return false;
        }

        Detail = result.Value;
        await LoadTabsAsync();
        return true;
    }

    protected override object CaptureState() => 0;

    protected override Task<bool> SaveCoreAsync() => Task.FromResult(true);

    /// <summary>Vuelve a leer el encabezado y las pestañas (tras editar, registrar o anular un abono).</summary>
    public Task RefreshAsync() => LoadAsync(_customerId);

    private async Task LoadTabsAsync()
    {
        await LoadReceivablesAsync();
        await LoadPaymentsAsync();
    }

    private async Task LoadPaymentsAsync()
    {
        var query = new ListCustomerPaymentsQuery(_customerId);
        var (completed, result) = await _runner.RunAsync(
            "ListarAbonosDelCliente",
            () => _useCases.RunAsync<ListCustomerPaymentsHandler, Result<CustomerPaymentPage>>(h => h.HandleAsync(query, CancellationToken.None)),
            new Dictionary<string, object?> { ["CustomerId"] = _customerId });
        if (!completed || result is not { IsSuccess: true })
        {
            return;
        }

        Payments.Clear();
        foreach (var item in result.Value.Items)
        {
            Payments.Add(new CustomerPaymentItem(item));
        }

        HasNoPayments = Payments.Count == 0;
    }

    [RelayCommand]
    private async Task RegisterPaymentAsync()
    {
        if (Detail is not { } detail || ActiveDialog is not null)
        {
            return;
        }

        ActiveDialog = new RegisterPaymentViewModel(
            _useCases,
            _runner,
            Dialogs,
            _printing,
            detail.Id,
            detail.BalanceCents,
            await IsShiftAvailableAsync(),
            OnDialogFinishedAsync,
            () => ActiveDialog = null);
    }

    [RelayCommand(CanExecute = nameof(HasSelectedPayment))]
    private async Task ReprintReceiptAsync()
    {
        if (SelectedPayment is { } payment && _printing is not null)
        {
            await _printing.PrintAsync(PrintSource.CustomerPayment(payment.Item.PaymentId), isReprint: true, payment.Folio, automatic: false);
        }
    }

    [RelayCommand(CanExecute = nameof(CanVoidSelected))]
    private void VoidPayment()
    {
        if (SelectedPayment is not { IsVoided: false } payment || ActiveDialog is not null)
        {
            return;
        }

        ActiveDialog = new VoidPaymentViewModel(_useCases, _runner, _authorization, payment.Item, OnDialogFinishedAsync, () => ActiveDialog = null);
    }

    private bool HasSelectedPayment() => SelectedPayment is not null;

    private bool CanVoidSelected() => SelectedPayment is { IsVoided: false };

    /// <summary>Hay turno abierto (o Turnos no tiene licencia): sin él el diálogo deshabilita "Registrar".</summary>
    private async Task<bool> IsShiftAvailableAsync()
    {
        if (_license?.IsModuleActive(LicensedModule.CashShifts) == false)
        {
            return true;
        }

        var (completed, result) = await _runner.RunAsync(
            "ConsultarTurnoActual",
            () => _useCases.RunAsync<GetCurrentShiftHandler, Result<CurrentShiftSummary?>>(h => h.HandleAsync(CancellationToken.None)));
        return !completed || result is not { IsSuccess: true } || result.Value is not null;
    }

    private async Task LoadReceivablesAsync()
    {
        var query = new ListCustomerReceivablesQuery(_customerId);
        var (completed, result) = await _runner.RunAsync(
            "ListarVentasACreditoDelCliente",
            () => _useCases.RunAsync<ListCustomerReceivablesHandler, Result<ReceivablePage>>(h => h.HandleAsync(query, CancellationToken.None)),
            new Dictionary<string, object?> { ["CustomerId"] = _customerId });
        if (!completed || result is not { IsSuccess: true })
        {
            return;
        }

        Receivables.Clear();
        foreach (var item in result.Value.Items)
        {
            Receivables.Add(new CustomerReceivableRow(item));
        }

        HasNoReceivables = Receivables.Count == 0;
    }

    /// <summary>Abre el detalle de venta existente de la fila seleccionada (contracts/ui.md "Ficha").</summary>
    [RelayCommand(CanExecute = nameof(HasSelectedReceivable))]
    private async Task OpenSaleAsync()
    {
        if (SelectedReceivable is not { } row || _saleDetailFactory is null || ActiveDialog is not null)
        {
            return;
        }

        var detail = _saleDetailFactory();
        detail.ShowCustomerLink = false;
        detail.Closed += (_, _) => _ = OnDialogFinishedAsync();
        if (await detail.LoadAsync(row.SaleId))
        {
            ActiveDialog = detail;
        }
    }

    private bool HasSelectedReceivable() => SelectedReceivable is not null;

    [RelayCommand]
    private void Edit()
    {
        if (Detail is not { } detail || ActiveDialog is not null)
        {
            return;
        }

        var form = _formFactory();
        form.Fill(detail);
        form.Saved += (_, _) => _ = OnDialogFinishedAsync();
        form.Closed += (_, _) => ActiveDialog = null;
        ActiveDialog = form;
    }

    [RelayCommand]
    private async Task DeactivateAsync()
    {
        if (Detail is not { } detail
            || !await Dialogs.ConfirmAsync(
                Strings.Customer_DeactivateTitle,
                string.Format(Display, Strings.Customer_DeactivateQuestion, detail.Name),
                Strings.Customer_Deactivate))
        {
            return;
        }

        await SetActiveAsync(detail, active: false);
    }

    [RelayCommand]
    private async Task ActivateAsync()
    {
        if (Detail is { } detail)
        {
            await SetActiveAsync(detail, active: true);
        }
    }

    private async Task SetActiveAsync(CustomerDetailDto detail, bool active)
    {
        ErrorMessage = null;
        var command = new SetCustomerActiveCommand(detail.Id, detail.Version, active);
        var (completed, result) = await _runner.RunAsync(
            active ? "ActivarCliente" : "DesactivarCliente",
            () => _useCases.RunAsync<SetCustomerActiveHandler, Result>(h => h.HandleAsync(command, CancellationToken.None)),
            new Dictionary<string, object?> { ["CustomerId"] = detail.Id });

        if (!completed || result is null)
        {
            return;
        }

        switch (result.Error)
        {
            case null:
                await RefreshAsync();
                break;
            case CustomerHasBalance balance:
                ErrorMessage = string.Format(Display, Strings.Customer_HasBalance, MoneyConverter.Format(balance.BalanceCents));
                break;
            case Conflict:
                await RefreshAsync();
                break;
            case Forbidden:
                ErrorMessage = Strings.Common_Forbidden;
                break;
            case ModuleNotLicensed:
                ErrorMessage = Strings.License_ModuleNotLicensed;
                break;
            default:
                await Dialogs.ShowMessageAsync(Strings.Common_ErrorTitle, Strings.Common_UnexpectedError);
                break;
        }
    }

    private async Task OnDialogFinishedAsync()
    {
        ActiveDialog = null;
        await RefreshAsync();
    }
}
