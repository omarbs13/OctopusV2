using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Users;
using Pos.Application.Users.AuthorizeAdmin;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;
using Pos.Domain.Users;

namespace Pos.Desktop.Auth;

/// <summary>
/// Diálogo de autorización de administrador (Historia 7): el administrador captura su usuario y
/// contraseña sin cerrar la sesión del cajero. Devuelve la concesión de un solo uso.
/// </summary>
public sealed partial class AdminAuthorizationViewModel : ViewModelBase
{
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly Permission _permission;
    private readonly Action<Guid?> _finished;

    public AdminAuthorizationViewModel(
        UseCases useCases,
        OperationRunner runner,
        Permission permission,
        Action<Guid?> finished)
    {
        _useCases = useCases;
        _runner = runner;
        _permission = permission;
        _finished = finished;
    }

    public string Message => string.Format(
        System.Globalization.CultureInfo.CurrentCulture,
        Strings.Auth_AuthorizeMessage,
        _permission switch
        {
            Permission.CancelSales => Strings.Auth_OperationCancelSale,
            Permission.WithdrawCash => Strings.Auth_OperationWithdrawCash,
            Permission.ApproveReturns => Strings.Auth_OperationReturns,
            Permission.ApproveCreditOverLimit => Strings.Auth_OperationCreditOverLimit,
            Permission.VoidCustomerPayments => Strings.Auth_OperationVoidPayment,
            _ => Strings.Auth_OperationOpenDrawer,
        });

    [ObservableProperty]
    public partial string UserName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AuthorizeCommand))]
    public partial bool IsBusy { get; private set; }

    /// <summary>La vista lleva el foco a la contraseña.</summary>
    public event EventHandler? FocusPasswordRequested;

    [RelayCommand(CanExecute = nameof(CanAuthorize))]
    private async Task AuthorizeAsync()
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var command = new AuthorizeAdminCommand(_permission, UserName, Password);
            var (completed, result) = await _runner.RunAsync(
                "AutorizarAdministrador",
                () => _useCases.RunAsync<AuthorizeAdminHandler, Result<Guid>>(h => h.HandleAsync(command, CancellationToken.None)),
                new Dictionary<string, object?> { ["Permission"] = _permission.ToString() });
            if (!completed || result is null)
            {
                return;
            }

            if (result.IsSuccess)
            {
                Password = string.Empty;
                _finished(result.Value);
                return;
            }

            Password = string.Empty;
            ErrorMessage = result.Error switch
            {
                LockedOut locked => UserMessages.LockedOut(locked.UntilUtc, DateTime.UtcNow),
                InvalidCredentials => Strings.Auth_AuthorizeFailed,
                ValidationFailed => Strings.Auth_AdminCredentialsMissing,
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
    private void Cancel() => _finished(null);

    private bool CanAuthorize() => !IsBusy;
}
