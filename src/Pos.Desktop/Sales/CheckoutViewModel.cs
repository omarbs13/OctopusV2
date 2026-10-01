using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.CreditNotes;
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

    // lookupCreditNote consulta el saldo de una nota por folio (013); es nulo si el módulo Devoluciones
    // no está activo y entonces el cobro no ofrece la nota de crédito.
    public CheckoutViewModel(
        Checkout checkout,
        Func<CheckoutViewModel, Task> confirm,
        Action cancel,
        Func<string, Task<(CreditNoteBalance? Balance, string? Error)>>? lookupCreditNote = null)
    {
        ArgumentNullException.ThrowIfNull(checkout);
        _checkout = checkout;
        _confirm = confirm;
        _cancel = cancel;
        _lookupCreditNote = lookupCreditNote;
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
    public bool CanUseCreditNote => _lookupCreditNote is not null;

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
        foreach (var payment in _checkout.Payments)
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
