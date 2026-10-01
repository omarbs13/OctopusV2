using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.CashShifts;
using Pos.Application.Printing.PrintTicket;
using Pos.Application.Receivables;
using Pos.Application.Receivables.RegisterCustomerPayment;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;
using Pos.Desktop.Sales;
using Pos.Desktop.Settings;
using Pos.Domain.Common;
using Pos.Domain.Sales;

namespace Pos.Desktop.Customers;

/// <summary>
/// Registrar abono (contracts/ui.md "Registrar abono"): monto, forma de pago y referencia. El
/// <c>RequestId</c> se genera al abrir, así que un doble clic devuelve el mismo abono. Al registrar
/// imprime el recibo; si la impresión falla el abono no se revierte (research §12).
/// </summary>
public sealed partial class RegisterPaymentViewModel : ViewModelBase
{
    private static readonly CultureInfo Display = MoneyConverter.Culture;

    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly IDialogService _dialogs;
    private readonly TicketPrintingService? _printing;
    private readonly Guid _customerId;
    private readonly Func<Task> _finished;
    private readonly Action _closed;
    private readonly Guid _requestId = Guid.CreateVersion7();

    public RegisterPaymentViewModel(
        UseCases useCases,
        OperationRunner runner,
        IDialogService dialogs,
        TicketPrintingService? printing,
        Guid customerId,
        long balanceCents,
        bool shiftAvailable,
        Func<Task> finished,
        Action closed)
    {
        _useCases = useCases;
        _runner = runner;
        _dialogs = dialogs;
        _printing = printing;
        _customerId = customerId;
        _finished = finished;
        _closed = closed;
        BalanceText = string.Format(Display, Strings.Payment_CurrentBalance, MoneyConverter.Format(balanceCents));
        Methods =
        [
            new PaymentMethodOption(PaymentMethod.Cash, PaymentMethodLabels.Of(PaymentMethod.Cash)),
            new PaymentMethodOption(PaymentMethod.Card, PaymentMethodLabels.Of(PaymentMethod.Card)),
            new PaymentMethodOption(PaymentMethod.Transfer, PaymentMethodLabels.Of(PaymentMethod.Transfer)),
        ];
        SelectedMethod = Methods[0];
        IsShiftMissing = !shiftAvailable;
        ErrorMessage = shiftAvailable ? null : Strings.Payment_ShiftRequired;
    }

    public string BalanceText { get; }

    public IReadOnlyList<PaymentMethodOption> Methods { get; }

    [ObservableProperty]
    public partial string AmountText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial PaymentMethodOption SelectedMethod { get; set; }

    [ObservableProperty]
    public partial string Reference { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? AmountError { get; private set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    /// <summary>Sin turno abierto "Registrar" queda deshabilitado (FR-012).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    public partial bool IsShiftMissing { get; private set; }

    /// <summary>"Registrar" se deshabilita mientras se procesa.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    public partial bool IsBusy { get; private set; }

    private bool CanRegister() => !IsBusy && !IsShiftMissing;

    [RelayCommand(CanExecute = nameof(CanRegister))]
    private async Task RegisterAsync()
    {
        AmountError = null;
        ErrorMessage = null;
        if (!Money.TryParse(AmountText, out var amount))
        {
            AmountError = Strings.Payment_AmountInvalid;
            return;
        }

        IsBusy = true;
        try
        {
            var command = new RegisterCustomerPaymentCommand(_requestId, _customerId, amount.Cents, SelectedMethod.Method, Reference);
            var (completed, result) = await _runner.RunAsync(
                "RegistrarAbono",
                () => _useCases.RunAsync<RegisterCustomerPaymentHandler, Result<PaymentReceipt>>(h => h.HandleAsync(command, CancellationToken.None)),
                new Dictionary<string, object?> { ["CustomerId"] = _customerId, ["RequestId"] = _requestId });
            if (!completed || result is null)
            {
                return;
            }

            if (result.IsSuccess)
            {
                await PrintReceiptAsync(result.Value);
                await _finished();
                return;
            }

            ShowError(result.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Close() => _closed();

    private async Task PrintReceiptAsync(PaymentReceipt receipt)
    {
        var destination = _printing is null
            ? null
            : await _printing.PrintAsync(PrintSource.CustomerPayment(receipt.PaymentId), isReprint: false, receipt.Folio, automatic: true);
        if (destination is null)
        {
            await _dialogs.ShowMessageAsync(Strings.Common_InfoTitle, Strings.Payment_PrintFailed);
        }
    }

    private void ShowError(Error error)
    {
        switch (error)
        {
            case PaymentExceedsBalance exceeds:
                AmountError = string.Format(Display, Strings.Payment_ExceedsBalance, MoneyConverter.Format(exceeds.MaxCents));
                break;
            case ShiftRequired:
                ErrorMessage = Strings.Payment_ShiftRequired;
                IsShiftMissing = true;
                break;
            case ShiftOwnedByOther other:
                ErrorMessage = CashShiftMessages.ShiftOwnedByOther(other.OpenedByName);
                IsShiftMissing = true;
                break;
            case ValidationFailed validation:
                ErrorMessage = validation.Errors is [{ } first, ..] ? first.Message : Strings.Common_UnexpectedError;
                break;
            case ModuleNotLicensed:
                ErrorMessage = Strings.License_ModuleNotLicensed;
                break;
            case Forbidden:
                ErrorMessage = Strings.Common_Forbidden;
                break;
            case NotFound:
                ErrorMessage = Strings.Customer_NotFound;
                break;
            default:
                ErrorMessage = Strings.Common_UnexpectedError;
                break;
        }
    }
}
