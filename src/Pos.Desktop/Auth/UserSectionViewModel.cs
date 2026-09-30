using CommunityToolkit.Mvvm.Input;
using Pos.Application.Users.Session;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;
using Pos.Desktop.Shell;
using Pos.Domain.Users;

namespace Pos.Desktop.Auth;

/// <summary>Acción del menú de usuario: texto y comando.</summary>
public sealed record UserMenuAction(string Title, IAsyncRelayCommand Command);

/// <summary>
/// Sección de usuario del menú (FR-009): nombre y rol (o iniciales con tooltip si el menú está
/// contraído) y las acciones de cambiar contraseña, cambiar de usuario y cerrar sesión.
/// </summary>
public sealed class UserSectionViewModel : ViewModelBase
{
    private readonly ISessionActions _actions;
    private readonly ModalHost _modal;
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;

    public UserSectionViewModel(
        IUserSession session,
        ISessionActions actions,
        ModalHost modal,
        UseCases useCases,
        OperationRunner runner)
    {
        ArgumentNullException.ThrowIfNull(session);
        _actions = actions;
        _modal = modal;
        _useCases = useCases;
        _runner = runner;

        var user = session.User;
        DisplayName = user?.FullName ?? string.Empty;
        Initials = user?.Initials ?? "?";
        RoleText = user?.Role == UserRole.Admin ? Strings.Role_Admin : Strings.Role_Cashier;
        Tooltip = $"{DisplayName} · {RoleText}";

        Actions =
        [
            new UserMenuAction(Strings.Session_ChangePassword, new AsyncRelayCommand(ChangePasswordAsync)),
            new UserMenuAction(Strings.Session_SwitchUser, new AsyncRelayCommand(() => _actions.SwitchUserAsync())),
            new UserMenuAction(Strings.Session_Logout, new AsyncRelayCommand(() => _actions.LogoutAsync())),
        ];
    }

    public string DisplayName { get; }

    public string RoleText { get; }

    public string Initials { get; }

    /// <summary>"Nombre completo · Rol", para el tooltip del menú contraído.</summary>
    public string Tooltip { get; }

    public IReadOnlyList<UserMenuAction> Actions { get; }

    private Task ChangePasswordAsync()
    {
        _modal.Show(new ChangePasswordViewModel(
            _useCases,
            _runner,
            sealedUserId: null,
            completed: () =>
            {
                _modal.Close();
                return Task.CompletedTask;
            },
            cancelled: () =>
            {
                _modal.Close();
                return Task.CompletedTask;
            }));
        return Task.CompletedTask;
    }
}
