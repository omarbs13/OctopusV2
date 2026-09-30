using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.CashShifts;
using Pos.Application.CashShifts.RegisterCashMovement;
using Pos.Application.Printing.PrintTicket;
using Pos.Desktop.Auth;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;
using Pos.Desktop.Settings;
using Pos.Domain.CashShifts;
using Pos.Domain.Common;
using Pos.Domain.Users;

namespace Pos.Desktop.CashShifts;

/// <summary>
/// Diálogo de ingreso o retiro de efectivo (Historia 4): el tipo lo fija el botón que lo abrió. El
/// retiro de un cajero pide la autorización de un administrador con el mecanismo de 007 (FR-011). Al
/// terminar muestra el folio y permite imprimir el comprobante (FR-012).
/// </summary>
public sealed partial class CashMovementViewModel : ViewModelBase
{
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly IDialogService _dialogs;
    private readonly TicketPrintingService _printing;
    private readonly AdminAuthorizationService _authorization;
    private readonly Guid _shiftId;
    private readonly Action _reshow;
    private readonly Action<bool> _finished;

    private RegisteredMovement? _registered;

    public CashMovementViewModel(
        UseCases useCases,
        OperationRunner runner,
        IDialogService dialogs,
        TicketPrintingService printing,
        AdminAuthorizationService authorization,
        Guid shiftId,
        CashMovementType type,
        Action reshow,
        Action<bool> finished)
    {
        _useCases = useCases;
        _runner = runner;
        _dialogs = dialogs;
        _printing = printing;
        _authorization = authorization;
        _shiftId = shiftId;
        Type = type;
        _reshow = reshow;
        _finished = finished;
    }

    public CashMovementType Type { get; }

    public string Title => Type == CashMovementType.In ? Strings.CashMovement_DepositTitle : Strings.CashMovement_WithdrawalTitle;

    [ObservableProperty]
    public partial string AmountText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string Reason { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? AmountError { get; private set; }

    [ObservableProperty]
    public partial string? ReasonError { get; private set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IsBusy { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditing), nameof(DoneText))]
    public partial bool IsDone { get; private set; }

    public bool IsEditing => !IsDone;

    public string DoneText => _registered is null
        ? string.Empty
        : string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.CashMovement_Registered, _registered.Folio);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        AmountError = null;
        ReasonError = null;
        ErrorMessage = null;

        var parsed = Money.Parse(AmountText);
        if (parsed.Value is not { } amount || amount.Cents <= 0)
        {
            AmountError = parsed.Error == MoneyParseError.TooLarge ? CashShiftMessages.AmountTooLarge : Strings.CashMovement_AmountInvalid;
            return;
        }

        IsBusy = true;
        try
        {
            var result = await RegisterAsync(amount.Cents, grantId: null);
            if (result is { Error: Forbidden { CanBeAuthorized: true } })
            {
                // Retiro de un cajero: se ofrece la autorización de un administrador sin cerrar su sesión (FR-011).
                var request = await _dialogs.AskAsync(
                    Strings.Auth_AuthorizeTitle,
                    Strings.Auth_RequestAuthorizationQuestion,
                    Strings.Auth_RequestAuthorization,
                    Strings.Common_Cancel);
                var grant = request ? await _authorization.RequestAsync(Permission.WithdrawCash) : null;
                _reshow();
                if (grant is not { } grantId)
                {
                    return;
                }

                result = await RegisterAsync(amount.Cents, grantId);
            }

            if (result is null)
            {
                return;
            }

            switch (result.Error)
            {
                case null:
                    _registered = result.Value;
                    IsDone = true;
                    break;

                case ValidationFailed validation:
                    ApplyValidation(validation);
                    break;

                case InsufficientCash insufficient:
                    // El cajero recibe un rechazo genérico sin montos; el administrador ve el disponible (FR-010).
                    ErrorMessage = insufficient.AvailableCents is { } available
                        ? CashShiftMessages.WithdrawalExceedsCashWithAvailable(MoneyConverter.Format(available))
                        : CashShiftMessages.WithdrawalExceedsCash;
                    break;

                case ShiftClosed or NotFound:
                    await _dialogs.ShowMessageAsync(Strings.Common_InfoTitle, CashShiftMessages.ShiftClosed);
                    _finished(false);
                    break;

                case Forbidden:
                    ErrorMessage = Strings.Common_Forbidden;
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
    private async Task PrintAsync()
    {
        if (_registered is { } movement)
        {
            await _printing.PrintAsync(PrintSource.CashMovement(movement.MovementId), isReprint: false, movement.Folio, automatic: false);
        }
    }

    [RelayCommand]
    private void Close() => _finished(IsDone);

    private bool CanSave() => !IsBusy && !string.IsNullOrWhiteSpace(Reason);

    private void ApplyValidation(ValidationFailed validation)
    {
        foreach (var error in validation.Errors)
        {
            if (error.Field == CashShiftFields.Reason)
            {
                ReasonError = error.Message;
            }
            else if (error.Field == CashShiftFields.Amount)
            {
                AmountError = error.Message;
            }
            else
            {
                ErrorMessage = error.Message;
            }
        }
    }

    private async Task<Result<RegisteredMovement>?> RegisterAsync(long amountCents, Guid? grantId)
    {
        var command = new RegisterCashMovementCommand(_shiftId, Type, amountCents, Reason, grantId);
        var (completed, result) = await _runner.RunAsync(
            "RegistrarMovimientoDeEfectivo",
            () => _useCases.RunAsync<RegisterCashMovementHandler, Result<RegisteredMovement>>(h => h.HandleAsync(command, CancellationToken.None)),
            new Dictionary<string, object?> { ["ShiftId"] = _shiftId, ["Type"] = Type.ToString(), ["Authorized"] = grantId is not null });
        return completed ? result : null;
    }
}
