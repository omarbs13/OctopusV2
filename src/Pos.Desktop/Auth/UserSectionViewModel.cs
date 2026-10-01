using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Users.Session;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;
using Pos.Desktop.Shell;
using Pos.Domain.Users;

namespace Pos.Desktop.Auth;

/// <summary>Acción del menú de usuario: texto y comando, o un submenú de opciones excluyentes (por ejemplo, el tema).</summary>
public sealed record UserMenuAction(string Title, IAsyncRelayCommand? Command = null, IReadOnlyList<UserMenuChoice>? Choices = null);

/// <summary>Opción de un submenú del menú de usuario; la elegida aparece marcada.</summary>
public sealed partial class UserMenuChoice : ObservableObject
{
    public UserMenuChoice(string title, IRelayCommand command, bool isChecked)
    {
        Title = title;
        Command = command;
        IsChecked = isChecked;
    }

    public string Title { get; }

    public IRelayCommand Command { get; }

    [ObservableProperty]
    public partial bool IsChecked { get; set; }
}

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
    private readonly ThemeService _theme;
    private readonly UserMenuChoice[] _themeChoices;

    public UserSectionViewModel(
        IUserSession session,
        ISessionActions actions,
        ModalHost modal,
        UseCases useCases,
        OperationRunner runner,
        ThemeService theme)
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

        var light = new UserMenuChoice(Strings.Session_ThemeLight, new RelayCommand(() => ChooseTheme(AppTheme.Light)), ThemeService.Current == AppTheme.Light);
        var dark = new UserMenuChoice(Strings.Session_ThemeDark, new RelayCommand(() => ChooseTheme(AppTheme.Dark)), ThemeService.Current == AppTheme.Dark);
        _themeChoices = [light, dark];
        _theme = theme;

        Actions =
        [
            new UserMenuAction(Strings.Session_Theme, Choices: _themeChoices),
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

    /// <summary>Aplica y guarda el tema, y marca solo la opción elegida.</summary>
    private void ChooseTheme(AppTheme theme)
    {
        _theme.Set(theme);
        _themeChoices[0].IsChecked = theme == AppTheme.Light;
        _themeChoices[1].IsChecked = theme == AppTheme.Dark;
    }

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
