using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Users;
using Pos.Application.Users.VerifySessionPassword;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;

namespace Pos.Desktop.Shell;

/// <summary>
/// Capa de bloqueo por inactividad (FR-023): cubre la sesión sin desecharla y pide la contraseña
/// del mismo usuario. Los fallos cuentan para el bloqueo de FR-005.
/// </summary>
public sealed partial class LockViewModel : ViewModelBase
{
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly Func<Task> _unlocked;
    private readonly Func<Task<bool>> _switchUser;

    public LockViewModel(
        UseCases useCases,
        OperationRunner runner,
        string displayName,
        Func<Task> unlocked,
        Func<Task<bool>> switchUser)
    {
        _useCases = useCases;
        _runner = runner;
        DisplayName = displayName;
        _unlocked = unlocked;
        _switchUser = switchUser;
    }

    public string DisplayName { get; }

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UnlockCommand))]
    public partial bool IsBusy { get; private set; }

    /// <summary>La vista lleva el foco a la contraseña.</summary>
    public event EventHandler? FocusPasswordRequested;

    [RelayCommand(CanExecute = nameof(CanUnlock))]
    private async Task UnlockAsync()
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var password = Password;
            var (completed, result) = await _runner.RunAsync(
                "DesbloquearSesion",
                () => _useCases.RunAsync<VerifySessionPasswordHandler, Result>(h => h.HandleAsync(password, CancellationToken.None)));
            if (!completed || result is null)
            {
                return;
            }

            Password = string.Empty;
            if (result.IsSuccess)
            {
                await _unlocked();
                return;
            }

            ErrorMessage = result.Error switch
            {
                LockedOut locked => UserMessages.LockedOut(locked.UntilUtc, DateTime.UtcNow),
                InvalidCredentials => Strings.Auth_PasswordIncorrect,
                _ => Strings.Common_UnexpectedError,
            };
            FocusPasswordRequested?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SwitchUserAsync() => await _switchUser();

    private bool CanUnlock() => !IsBusy;
}
