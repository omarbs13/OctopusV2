using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.CashShifts;
using Pos.Application.Receivables;
using Pos.Application.Receivables.VoidCustomerPayment;
using Pos.Desktop.Auth;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;
using Pos.Desktop.Sales;
using Pos.Domain.Users;

namespace Pos.Desktop.Customers;

/// <summary>
/// Anular abono (contracts/ui.md "Anular abono"): resumen y motivo. Al confirmar pide siempre la
/// autorización de un Administrador, también si quien opera lo es (FR-014). Los rechazos por efectivo
/// no revelan montos.
/// </summary>
public sealed partial class VoidPaymentViewModel : ViewModelBase
{
    private static readonly CultureInfo Display = MoneyConverter.Culture;

    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly AdminAuthorizationService? _authorization;
    private readonly CustomerPaymentRowDto _payment;
    private readonly Func<Task> _finished;
    private readonly Action _closed;

    public VoidPaymentViewModel(
        UseCases useCases,
        OperationRunner runner,
        AdminAuthorizationService? authorization,
        CustomerPaymentRowDto payment,
        Func<Task> finished,
        Action closed)
    {
        _useCases = useCases;
        _runner = runner;
        _authorization = authorization;
        _payment = payment;
        _finished = finished;
        _closed = closed;
        SummaryText = string.Format(
            Display,
            Strings.Payment_VoidSummary,
            payment.Folio,
            payment.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
            MoneyConverter.Format(payment.AmountCents),
            PaymentMethodLabels.Of(payment.Method));
    }

    public string SummaryText { get; }

    [ObservableProperty]
    public partial string Reason { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ReasonError { get; private set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial bool IsBusy { get; private set; }

    private bool CanConfirm() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private async Task ConfirmAsync()
    {
        ReasonError = ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(Reason))
        {
            ReasonError = ReceivableMessages.ReasonRequired;
            return;
        }

        if (_authorization is null || await _authorization.RequestAsync(Permission.VoidCustomerPayments) is not { } grant)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var command = new VoidCustomerPaymentCommand(_payment.PaymentId, Reason, grant);
            var (completed, result) = await _runner.RunAsync(
                "AnularAbono",
                () => _useCases.RunAsync<VoidCustomerPaymentHandler, Result>(h => h.HandleAsync(command, CancellationToken.None)),
                new Dictionary<string, object?> { ["PaymentId"] = _payment.PaymentId });
            if (!completed || result is null)
            {
                return;
            }

            if (result.IsSuccess)
            {
                await _finished();
                return;
            }

            ErrorMessage = result.Error switch
            {
                InvalidState state when state.Message == ReceivableMessages.ReturnedAfter => Strings.Payment_ReturnedAfter,
                InvalidState => Strings.Payment_AlreadyVoided,
                ShiftRequired => Strings.Payment_ShiftRequired,
                ShiftOwnedByOther other => CashShiftMessages.ShiftOwnedByOther(other.OpenedByName),
                InsufficientCash => Strings.Payment_InsufficientCash,
                ValidationFailed validation => validation.Errors is [{ } first, ..] ? first.Message : Strings.Common_UnexpectedError,
                ModuleNotLicensed => Strings.License_ModuleNotLicensed,
                Forbidden => Strings.Common_Forbidden,
                _ => Strings.Common_UnexpectedError,
            };
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Close() => _closed();
}
