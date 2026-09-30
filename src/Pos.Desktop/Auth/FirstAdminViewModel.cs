using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Users;
using Pos.Application.Users.CreateFirstAdmin;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;

namespace Pos.Desktop.Auth;

/// <summary>
/// Asistente de primer arranque (FR-001): crea el primer Administrador. No se puede omitir; al
/// terminar pasa al inicio de sesión con el usuario escrito.
/// </summary>
public sealed partial class FirstAdminViewModel : ViewModelBase
{
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly IDialogService _dialogs;
    private readonly Func<string, Task> _created;
    private readonly Func<Task> _alreadyDone;

    public FirstAdminViewModel(
        UseCases useCases,
        OperationRunner runner,
        IDialogService dialogs,
        Func<string, Task> created,
        Func<Task> alreadyDone)
    {
        _useCases = useCases;
        _runner = runner;
        _dialogs = dialogs;
        _created = created;
        _alreadyDone = alreadyDone;
    }

    [ObservableProperty]
    public partial string FullName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string UserName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ConfirmPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? FullNameError { get; private set; }

    [ObservableProperty]
    public partial string? UserNameError { get; private set; }

    [ObservableProperty]
    public partial string? PasswordError { get; private set; }

    [ObservableProperty]
    public partial string? ConfirmPasswordError { get; private set; }

    /// <summary>Campo que debe recibir el foco (el primero con error).</summary>
    [ObservableProperty]
    public partial string? FocusField { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    public partial bool IsBusy { get; private set; }

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateAsync()
    {
        FullNameError = UserNameError = PasswordError = ConfirmPasswordError = FocusField = null;
        IsBusy = true;
        try
        {
            var command = new CreateFirstAdminCommand(FullName, UserName, Password, ConfirmPassword);
            var (completed, result) = await _runner.RunAsync(
                "CrearPrimerAdministrador",
                () => _useCases.RunAsync<CreateFirstAdminHandler, Result>(h => h.HandleAsync(command, CancellationToken.None)));
            if (!completed || result is null)
            {
                return;
            }

            switch (result.Error)
            {
                case null:
                    Password = ConfirmPassword = string.Empty;
                    await _created(UserName.Trim());
                    break;

                case ValidationFailed validation:
                    ShowErrors(validation);
                    break;

                case InvalidState:
                    await _dialogs.ShowMessageAsync(Strings.Common_InfoTitle, Strings.Auth_SetupAlreadyDone);
                    await _alreadyDone();
                    break;

                default:
                    await _dialogs.ShowMessageAsync(Strings.Common_ErrorTitle, Strings.Common_UnexpectedError);
                    break;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanCreate() => !IsBusy;

    private void ShowErrors(ValidationFailed validation)
    {
        foreach (var error in validation.Errors)
        {
            switch (error.Field)
            {
                case UserFields.FullName:
                    FullNameError = error.Message;
                    break;
                case UserFields.UserName:
                    UserNameError = error.Message;
                    break;
                case UserFields.Password:
                    PasswordError = error.Message;
                    break;
                case UserFields.ConfirmPassword:
                    ConfirmPasswordError = error.Message;
                    break;
            }
        }

        FocusField = validation.Errors.Count > 0 ? validation.Errors[0].Field : null;
    }
}
