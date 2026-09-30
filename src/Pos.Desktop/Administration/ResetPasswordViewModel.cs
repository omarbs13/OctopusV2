using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Users;
using Pos.Application.Users.ResetUserPassword;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;

namespace Pos.Desktop.Administration;

/// <summary>
/// Diálogo para restablecer la contraseña de otro usuario (FR-016): la nueva queda como temporal y el
/// usuario debe cambiarla al iniciar sesión.
/// </summary>
public sealed partial class ResetPasswordViewModel : ViewModelBase
{
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly Guid _userId;
    private readonly string _userName;
    private readonly Action _close;

    public ResetPasswordViewModel(UseCases useCases, OperationRunner runner, Guid userId, string userName, Action close)
    {
        _useCases = useCases;
        _runner = runner;
        _userId = userId;
        _userName = userName;
        _close = close;
    }

    public string Description => string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Reset_Description, _userName);

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ConfirmPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? PasswordError { get; private set; }

    [ObservableProperty]
    public partial string? ConfirmPasswordError { get; private set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    /// <summary>Se muestra tras restablecer: "{usuario} deberá cambiarla al iniciar sesión."</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditing))]
    public partial string? DoneMessage { get; private set; }

    public bool IsEditing => DoneMessage is null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial bool IsBusy { get; private set; }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private async Task ConfirmAsync()
    {
        PasswordError = ConfirmPasswordError = ErrorMessage = null;
        IsBusy = true;
        try
        {
            var command = new ResetUserPasswordCommand(_userId, Password, ConfirmPassword);
            var (completed, result) = await _runner.RunAsync(
                "RestablecerContrasena",
                () => _useCases.RunAsync<ResetUserPasswordHandler, Result>(h => h.HandleAsync(command, CancellationToken.None)),
                new Dictionary<string, object?> { ["TargetUserId"] = _userId });
            if (!completed || result is null)
            {
                return;
            }

            switch (result.Error)
            {
                case null:
                    Password = ConfirmPassword = string.Empty;
                    DoneMessage = string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Reset_Done, _userName);
                    break;

                case ValidationFailed validation:
                    foreach (var error in validation.Errors)
                    {
                        if (error.Field == UserFields.ConfirmPassword)
                        {
                            ConfirmPasswordError = error.Message;
                        }
                        else
                        {
                            PasswordError = error.Message;
                        }
                    }

                    break;

                case NotFound:
                    ErrorMessage = Strings.Editor_NotFound;
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
    private void Close() => _close();

    private bool CanConfirm() => !IsBusy;
}
