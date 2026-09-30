using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Users;
using Pos.Application.Users.ChangeOwnPassword;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;

namespace Pos.Desktop.Auth;

/// <summary>
/// Cambio de contraseña propia. Modo obligatorio (tras un restablecimiento o el alta): no pide la
/// contraseña actual, "Salir" regresa al inicio de sesión sin abrir la sesión. Modo voluntario: diálogo
/// con la contraseña actual (FR-025).
/// </summary>
public sealed partial class ChangePasswordViewModel : ViewModelBase
{
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly Guid? _sealedUserId;
    private readonly Func<Task> _completed;
    private readonly Func<Task> _cancelled;

    public ChangePasswordViewModel(
        UseCases useCases,
        OperationRunner runner,
        Guid? sealedUserId,
        Func<Task> completed,
        Func<Task> cancelled)
    {
        _useCases = useCases;
        _runner = runner;
        _sealedUserId = sealedUserId;
        _completed = completed;
        _cancelled = cancelled;
    }

    public bool IsMandatory => _sealedUserId is not null;

    public bool AsksCurrentPassword => !IsMandatory;

    public string SaveText => IsMandatory ? Strings.Auth_SaveAndContinue : Strings.Auth_Save;

    public string CancelText => IsMandatory ? Strings.Auth_Exit : Strings.Common_Cancel;

    [ObservableProperty]
    public partial string CurrentPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ConfirmPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? CurrentPasswordError { get; private set; }

    [ObservableProperty]
    public partial string? NewPasswordError { get; private set; }

    [ObservableProperty]
    public partial string? ConfirmPasswordError { get; private set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    public partial string? FocusField { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IsBusy { get; private set; }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        CurrentPasswordError = NewPasswordError = ConfirmPasswordError = ErrorMessage = FocusField = null;
        IsBusy = true;
        try
        {
            var command = new ChangeOwnPasswordCommand(
                IsMandatory ? null : CurrentPassword,
                NewPassword,
                ConfirmPassword,
                _sealedUserId);
            var (completed, result) = await _runner.RunAsync(
                "CambiarContrasena",
                () => _useCases.RunAsync<ChangeOwnPasswordHandler, Result>(h => h.HandleAsync(command, CancellationToken.None)));
            if (!completed || result is null)
            {
                return;
            }

            switch (result.Error)
            {
                case null:
                    CurrentPassword = NewPassword = ConfirmPassword = string.Empty;
                    await _completed();
                    break;

                case ValidationFailed validation:
                    ShowErrors(validation);
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
    private Task CancelAsync() => _cancelled();

    private bool CanSave() => !IsBusy;

    private void ShowErrors(ValidationFailed validation)
    {
        foreach (var error in validation.Errors)
        {
            switch (error.Field)
            {
                case UserFields.CurrentPassword:
                    CurrentPasswordError = error.Message;
                    break;
                case UserFields.ConfirmPassword:
                    ConfirmPasswordError = error.Message;
                    break;
                default:
                    NewPasswordError = error.Message;
                    break;
            }
        }

        FocusField = validation.Errors.Count > 0 ? validation.Errors[0].Field : null;
    }
}
