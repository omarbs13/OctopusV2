using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.CreditNotes;
using Pos.Application.Customers;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;
using Pos.Domain.Common;
using Pos.Domain.Sales;

namespace Pos.Desktop.Sales;

/// <summary>Pago no en efectivo ya agregado, con sus textos.</summary>
public sealed record CheckoutPaymentRow(CheckoutPayment Payment)
{
    public string MethodText => PaymentMethodLabels.Of(Payment.Method);

    public string AmountText => MoneyConverter.Format(Payment.Amount.Cents);

    public string ReferenceText => Payment.Reference ?? string.Empty;
}

/// <summary>
/// Cliente elegido para vender a crédito (014) y la consulta de su estado de crédito con el total de la
/// venta; el error ya viene en español.
/// </summary>
public sealed record CreditCheckoutOption(
    CustomerForSaleDto Customer,
    Func<long, Task<(CustomerCreditStatusDto? Status, string? Error)>> LoadStatus);

/// <summary>
/// Diálogo de cobro. Envuelve un <see cref="Checkout"/> de dominio: todos los importes, el cambio y
/// el faltante los calcula el dominio (Principio III); aquí solo se captura y se presenta.
/// </summary>
public sealed partial class CheckoutViewModel : ViewModelBase
{
    private static readonly CultureInfo Display = CultureInfo.GetCultureInfo("es-MX");

    private readonly Checkout _checkout;
    private readonly Func<CheckoutViewModel, Task> _confirm;
    private readonly Action _cancel;
    private readonly Func<string, Task<(CreditNoteBalance? Balance, string? Error)>>? _lookupCreditNote;
    private readonly CreditCheckoutOption? _credit;

    // lookupCreditNote consulta el saldo de una nota por folio (013); es nulo si el módulo Devoluciones
    // no está activo y entonces el cobro no ofrece la nota de crédito. credit es el cliente elegido para
    // vender a crédito (014); sin él no se ofrece "Venta a crédito".
    public CheckoutViewModel(
        Checkout checkout,
        Func<CheckoutViewModel, Task> confirm,
        Action cancel,
        Func<string, Task<(CreditNoteBalance? Balance, string? Error)>>? lookupCreditNote = null,
        CreditCheckoutOption? credit = null)
    {
        ArgumentNullException.ThrowIfNull(checkout);
        _checkout = checkout;
        _confirm = confirm;
        _cancel = cancel;
        _lookupCreditNote = lookupCreditNote;
        _credit = credit;
        MethodOptions =
        [
            new(PaymentMethod.Card, PaymentMethodLabels.Of(PaymentMethod.Card)),
            new(PaymentMethod.Transfer, PaymentMethodLabels.Of(PaymentMethod.Transfer)),
        ];
        SelectedMethod = MethodOptions[0];
        NonCashAmountText = _checkout.Pending.ToEditableString();
        Refresh();
    }

    /// <summary>Total de la venta; el cobro capturado se conserva mientras no cambie.</summary>
    public Money Total => _checkout.Total;

    public string TotalText => MoneyConverter.Format(_checkout.Total.Cents);

    public IReadOnlyList<PaymentMethodOption> MethodOptions { get; }

    /// <summary>La nota de crédito solo se ofrece con el módulo Devoluciones activo (contracts/ui.md §3).</summary>
    public bool CanUseCreditNote => _lookupCreditNote is not null && !IsOnAccount;

    /// <summary>"Venta a crédito" solo aparece con un cliente elegido (contracts/ui.md "Cobro").</summary>
    public bool CanSellOnCredit => _credit is not null;

    public string CreditCustomerText => _credit is null ? string.Empty : string.Format(Display, Strings.Credit_CustomerLabel, _credit.Customer.Name);

    /// <summary>Cliente de la venta, solo si se cobra a crédito.</summary>
    public Guid? CustomerId => IsOnAccount ? _credit?.Customer.Id : null;

    /// <summary>Cliente elegido en el punto de venta, aunque todavía no se cobre a crédito.</summary>
    public Guid? ChosenCustomerId => _credit?.Customer.Id;

    /// <summary>Concesión de <c>ApproveCreditOverLimit</c> para el reintento tras exceder el límite.</summary>
    public Guid? OverLimitGrantId { get; set; }

    /// <summary>Se cobra con un único pago a crédito por el total (FR-005).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNormalPayment), nameof(CanUseCreditNote), nameof(CustomerId))]
    public partial bool IsOnAccount { get; private set; }

    public bool IsNormalPayment => !IsOnAccount;

    [ObservableProperty]
    public partial string CreditBalanceText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string CreditLimitText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string CreditAvailableText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOverdue { get; private set; }

    [ObservableProperty]
    public partial string? ExceedText { get; private set; }

    [ObservableProperty]
    public partial string? CreditError { get; private set; }

    [ObservableProperty]
    public partial string CreditNoteFolioText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? CreditNoteInfo { get; private set; }

    [ObservableProperty]
    public partial string? CreditNoteError { get; private set; }

    public ObservableCollection<CheckoutPaymentRow> Payments { get; } = [];

    [ObservableProperty]
    public partial string ReceivedText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial PaymentMethodOption SelectedMethod { get; set; }

    [ObservableProperty]
    public partial string NonCashAmountText { get; set; }

    [ObservableProperty]
    public partial string NonCashReference { get; set; } = string.Empty;

    [ObservableProperty]
    public partial CheckoutPaymentRow? SelectedPayment { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string PaidText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string ShortfallText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string ChangeText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasShortfall { get; private set; }

    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    [ObservableProperty]
    public partial bool CanConfirm { get; private set; }

    /// <summary>Evento para que la vista lleve el foco al campo de efectivo.</summary>
    public event EventHandler? FocusReceivedRequested;

    partial void OnReceivedTextChanged(string value)
    {
        var text = value.Trim();
        if (text.Length == 0)
        {
            _checkout.SetCashReceived(Money.Zero);
            ErrorMessage = null;
        }
        else if (Money.Parse(text) is { Value: { } money })
        {
            _checkout.SetCashReceived(money);
            ErrorMessage = null;
        }
        else
        {
            _checkout.SetCashReceived(Money.Zero);
            ErrorMessage = Strings.Checkout_ReceivedInvalid;
        }

        Refresh();
    }

    /// <summary>Monto rápido: <c>exact</c> o un billete. Reemplaza lo recibido, no lo acumula.</summary>
    [RelayCommand]
    private void QuickAmount(string? parameter)
    {
        try
        {
            _checkout.QuickAmount(int.TryParse(parameter, CultureInfo.InvariantCulture, out var bill) ? bill : null);
        }
        catch (DomainException ex)
        {
            ErrorMessage = ex.Message;
            return;
        }

        ErrorMessage = null;
        ReceivedText = _checkout.Received.ToEditableString();
        Refresh();
    }

    [RelayCommand]
    private void AddPayment()
    {
        var text = NonCashAmountText.Trim();
        if (Money.Parse(text) is not { Value: { } amount })
        {
            ErrorMessage = Strings.Checkout_AmountInvalid;
            return;
        }

        try
        {
            _checkout.AddNonCash(SelectedMethod.Method, amount, NonCashReference);
        }
        catch (DomainException ex)
        {
            ErrorMessage = ex.Message;
            return;
        }

        ErrorMessage = null;
        NonCashReference = string.Empty;
        Refresh();
        NonCashAmountText = _checkout.Pending.ToEditableString();
    }

    /// <summary>Consulta el saldo del folio y agrega el pago por el menor entre el saldo y lo pendiente.</summary>
    [RelayCommand]
    private async Task ApplyCreditNoteAsync()
    {
        CreditNoteError = null;
        CreditNoteInfo = null;
        if (_lookupCreditNote is null)
        {
            return;
        }

        var (balance, error) = await _lookupCreditNote(CreditNoteFolioText);
        if (balance is null)
        {
            CreditNoteError = error ?? Strings.CreditNote_NotFound;
            return;
        }

        var pending = _checkout.Pending.Cents;
        if (pending <= 0)
        {
            return;
        }

        try
        {
            _checkout.AddNonCash(PaymentMethod.CreditNote, Money.FromCents(Math.Min(balance.BalanceCents, pending)), balance.Folio);
        }
        catch (DomainException ex)
        {
            CreditNoteError = ex.Message;
            return;
        }

        CreditNoteInfo = string.Format(Display, Strings.CreditNote_Balance, MoneyConverter.Format(balance.BalanceCents));
        CreditNoteFolioText = string.Empty;
        Refresh();
        NonCashAmountText = _checkout.Pending.ToEditableString();
    }

    [RelayCommand]
    private void RemovePayment()
    {
        if (SelectedPayment is not { } row)
        {
            return;
        }

        _checkout.RemovePayment(_checkout.Payments.ToList().IndexOf(row.Payment));
        ErrorMessage = null;
        Refresh();
        NonCashAmountText = _checkout.Pending.ToEditableString();
    }

    /// <summary>Reemplaza los pagos capturados por un único pago a crédito y muestra el estado del cliente.</summary>
    [RelayCommand]
    private async Task SellOnCreditAsync()
    {
        if (_credit is null)
        {
            return;
        }

        _checkout.SetOnAccount();
        IsOnAccount = true;
        ErrorMessage = null;
        ReceivedText = string.Empty;
        Refresh();

        // Saldo, límite, disponible y avisos: los calcula GetCustomerCreditStatus (Principio III).
        CreditError = null;
        var (status, error) = await _credit.LoadStatus(_checkout.Total.Cents);
        if (status is null)
        {
            CreditError = error;
            return;
        }

        CreditBalanceText = MoneyConverter.Format(status.BalanceCents);
        CreditLimitText = MoneyConverter.Format(status.LimitCents);
        CreditAvailableText = MoneyConverter.Format(status.AvailableCents);
        HasOverdue = status.HasOverdue;
        ExceedText = status.WouldExceedByCents > 0
            ? string.Format(Display, Strings.Credit_Exceeds, MoneyConverter.Format(status.WouldExceedByCents))
            : null;
    }

    /// <summary>Quita el pago a crédito para cobrar en efectivo, tarjeta o transferencia.</summary>
    [RelayCommand]
    private void PayOtherWay()
    {
        var index = _checkout.Payments.ToList().FindIndex(p => p.Method == PaymentMethod.OnAccount);
        if (index >= 0)
        {
            _checkout.RemovePayment(index);
        }

        IsOnAccount = false;
        OverLimitGrantId = null;
        ErrorMessage = CreditError = ExceedText = null;
        Refresh();
        NonCashAmountText = _checkout.Pending.ToEditableString();
        RequestFocus();
    }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private async Task ConfirmAsync()
    {
        ErrorMessage = null;
        await _confirm(this);
    }

    [RelayCommand]
    private void Cancel() => _cancel();

    /// <summary>Pagos a registrar, ya con el efectivo aplicado, recibido y cambio.</summary>
    public IReadOnlyList<PaymentEntry> ToPayments() => _checkout.ToPayments();

    /// <summary>Lleva el foco al efectivo; lo usa la vista al abrir y al quitar pagos.</summary>
    public void RequestFocus() => FocusReceivedRequested?.Invoke(this, EventArgs.Empty);

    private void Refresh()
    {
        Payments.Clear();
        foreach (var payment in _checkout.Payments.Where(p => p.Method != PaymentMethod.OnAccount))
        {
            Payments.Add(new CheckoutPaymentRow(payment));
        }

        var paid = _checkout.Received.Cents + _checkout.NonCashTotal.Cents;
        PaidText = MoneyConverter.Format(paid);
        HasShortfall = _checkout.Shortfall.Cents > 0;
        ShortfallText = string.Format(Display, Strings.Checkout_Shortfall, MoneyConverter.Format(_checkout.Shortfall.Cents));
        ChangeText = string.Format(Display, Strings.Checkout_Change, MoneyConverter.Format(_checkout.Change.Cents));
        CanConfirm = _checkout.CanConfirm;
    }
}
