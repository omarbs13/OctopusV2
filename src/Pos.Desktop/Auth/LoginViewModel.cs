using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Users;
using Pos.Application.Users.SignIn;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;

namespace Pos.Desktop.Auth;

/// <summary>
/// Inicio de sesión (FR-002): usuario y contraseña. Un error limpia la contraseña y devuelve el foco
/// a ella; el mensaje nunca indica cuál dato falló (FR-004).
/// </summary>
public sealed partial class LoginViewModel : ViewModelBase
{
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly Func<SignInOutcome, Task> _signedIn;

    public LoginViewModel(
        UseCases useCases,
        OperationRunner runner,
        Func<SignInOutcome, Task> signedIn,
        string? userName = null,
        string? licenseWarning = null)
    {
        _useCases = useCases;
        _runner = runner;
        _signedIn = signedIn;
        UserName = userName ?? string.Empty;
        LicenseWarning = licenseWarning;
    }

    /// <summary>Aviso rojo de licencia por vencer o vencida (011, FR-013); nulo si no aplica.</summary>
    public string? LicenseWarning { get; }

    [ObservableProperty]
    public partial string UserName { get; set; }

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? UserNameError { get; private set; }

    [ObservableProperty]
    public partial string? PasswordError { get; private set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SignInCommand))]
    public partial bool IsBusy { get; private set; }

    /// <summary>La vista lleva el foco a la contraseña.</summary>
    public event EventHandler? FocusPasswordRequested;

    /// <summary>La vista lleva el foco al usuario.</summary>
    public event EventHandler? FocusUserNameRequested;

    [RelayCommand(CanExecute = nameof(CanSignIn))]
    private async Task SignInAsync()
    {
        UserNameError = PasswordError = ErrorMessage = null;
        IsBusy = true;
        try
        {
            var command = new SignInCommand(UserName, Password);
            var (completed, result) = await _runner.RunAsync(
                "IniciarSesion",
                () => _useCases.RunAsync<SignInHandler, Result<SignInOutcome>>(h => h.HandleAsync(command, CancellationToken.None)));
            if (!completed || result is null)
            {
                return;
            }

            if (result.IsSuccess)
            {
                Password = string.Empty;
                await _signedIn(result.Value);
                return;
            }

            Password = string.Empty;
            switch (result.Error)
            {
                case ValidationFailed validation:
                    foreach (var error in validation.Errors)
                    {
                        if (error.Field == UserFields.UserName)
                        {
                            UserNameError = error.Message;
                        }
                        else
                        {
                            PasswordError = error.Message;
                        }
                    }

                    (UserNameError is not null ? FocusUserNameRequested : FocusPasswordRequested)?.Invoke(this, EventArgs.Empty);
                    break;

                case LockedOut locked:
                    ErrorMessage = UserMessages.LockedOut(locked.UntilUtc, DateTime.UtcNow);
                    FocusPasswordRequested?.Invoke(this, EventArgs.Empty);
                    break;

                case InvalidCredentials:
                    ErrorMessage = UserMessages.InvalidCredentials;
                    FocusPasswordRequested?.Invoke(this, EventArgs.Empty);
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

    private bool CanSignIn() => !IsBusy;
}
