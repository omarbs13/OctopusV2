using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.CashShifts;
using Pos.Application.CashShifts.OpenShift;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;
using Pos.Domain.Common;

namespace Pos.Desktop.CashShifts;

/// <summary>Diálogo de apertura de turno (Historia 1): captura el fondo inicial; con 0 pide confirmación explícita (FR-003).</summary>
public sealed partial class OpenShiftViewModel : ViewModelBase
{
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly IDialogService _dialogs;
    private readonly Action<bool> _finished;

    public OpenShiftViewModel(UseCases useCases, OperationRunner runner, IDialogService dialogs, Action<bool> finished)
    {
        _useCases = useCases;
        _runner = runner;
        _dialogs = dialogs;
        _finished = finished;
    }

    [ObservableProperty]
    public partial string FloatText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? FloatError { get; private set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial bool IsBusy { get; private set; }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private async Task ConfirmAsync()
    {
        FloatError = null;
        ErrorMessage = null;

        var parsed = Money.Parse(FloatText);
        if (parsed.Value is not { } amount)
        {
            FloatError = parsed.Error == MoneyParseError.TooLarge
                ? CashShiftMessages.AmountTooLarge
                : CashShiftMessages.OpeningFloatInvalid;
            return;
        }

        var confirmZero = false;
        if (amount.Cents == 0)
        {
            if (!await _dialogs.ConfirmAsync(Strings.Shift_ZeroFloatTitle, Strings.Shift_ZeroFloatQuestion, Strings.Shift_ZeroFloatConfirm))
            {
                return;
            }

            confirmZero = true;
        }

        IsBusy = true;
        try
        {
            var command = new OpenShiftCommand(amount.Cents, confirmZero);
            var (completed, result) = await _runner.RunAsync(
                "AbrirTurno",
                () => _useCases.RunAsync<OpenShiftHandler, Result<CurrentShiftSummary>>(h => h.HandleAsync(command, CancellationToken.None)),
                new Dictionary<string, object?> { ["OpeningFloatCents"] = amount.Cents });
            if (!completed || result is null)
            {
                return;
            }

            switch (result.Error)
            {
                case null:
                    _finished(true);
                    break;

                case ShiftAlreadyOpen:
                    await _dialogs.ShowMessageAsync(Strings.Common_InfoTitle, CashShiftMessages.ShiftAlreadyOpen);
                    _finished(true);
                    break;

                case ValidationFailed validation:
                    FloatError = validation.Errors is [{ } first, ..] ? first.Message : CashShiftMessages.OpeningFloatInvalid;
                    break;

                case Forbidden:
                    ErrorMessage = Strings.Common_Forbidden;
                    break;

                case ModuleNotLicensed:
                    ErrorMessage = Strings.License_ModuleNotLicensed;
                    break;

                default:
                    ErrorMessage = Strings.Common_UnexpectedError;
                    break;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel() => _finished(false);

    private bool CanConfirm() => !IsBusy;
}
