using CommunityToolkit.Mvvm.ComponentModel;
using Pos.Application.Abstractions;
using Pos.Application.Users;
using Pos.Application.Users.CreateUser;
using Pos.Application.Users.GetUser;
using Pos.Application.Users.Session;
using Pos.Application.Users.UpdateUser;
using Pos.Desktop.Common;
using Pos.Desktop.Forms;
using Pos.Desktop.Resources;
using Pos.Domain.Users;

namespace Pos.Desktop.Administration;

/// <summary>Opción del selector de rol.</summary>
public sealed record RoleOption(UserRole Role, string Label);

/// <summary>
/// Alta y edición de un usuario (formulario corto, FR-015). En el alta pide la contraseña inicial,
/// que es temporal. Uno mismo no puede cambiarse el rol ni desactivarse (FR-018).
/// </summary>
public sealed partial class UserEditorViewModel : FormViewModel<Guid>
{
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly IUserSession _session;

    private Guid? _userId;
    private int _expectedVersion;
    private bool _wasActive;
    private bool _hasHeldSale;

    public UserEditorViewModel(UseCases useCases, OperationRunner runner, IDialogService dialogs, IUserSession session)
        : base(dialogs)
    {
        _useCases = useCases;
        _runner = runner;
        _session = session;
        Roles =
        [
            new RoleOption(UserRole.Cashier, Strings.Role_Cashier),
            new RoleOption(UserRole.Admin, Strings.Role_Admin),
        ];
        SelectedRole = Roles[0];
        ResetOriginalState();
    }

    public IReadOnlyList<RoleOption> Roles { get; }

    [ObservableProperty]
    public partial string FullName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string UserName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial RoleOption SelectedRole { get; set; }

    [ObservableProperty]
    public partial bool IsActive { get; set; } = true;

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ConfirmPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? FullNameError { get; private set; }

    [ObservableProperty]
    public partial string? UserNameError { get; private set; }

    [ObservableProperty]
    public partial string? RoleError { get; private set; }

    [ObservableProperty]
    public partial string? PasswordError { get; private set; }

    [ObservableProperty]
    public partial string? ConfirmPasswordError { get; private set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(IsCreateMode))]
    public partial bool IsEditMode { get; private set; }

    /// <summary>Se edita al usuario conectado: el rol y el estado quedan bloqueados.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChangeRoleAndState))]
    public partial bool IsSelf { get; private set; }

    public bool IsCreateMode => !IsEditMode;

    public bool CanChangeRoleAndState => !IsSelf;

    public override string Title => IsEditMode ? Strings.UserEditor_EditTitle : Strings.UserEditor_NewTitle;

    /// <summary>Carga un usuario para editarlo. Devuelve falso si ya no existe (y lo informa).</summary>
    public async Task<bool> LoadAsync(Guid userId)
    {
        var (completed, result) = await _runner.RunAsync(
            "CargarUsuario",
            () => _useCases.RunAsync<GetUserHandler, Result<UserDto>>(h => h.HandleAsync(userId, CancellationToken.None)),
            new Dictionary<string, object?> { ["TargetUserId"] = userId });

        if (!completed || result is null)
        {
            return false;
        }

        if (!result.IsSuccess)
        {
            await Dialogs.ShowMessageAsync(Strings.Common_InfoTitle, result.Error is Forbidden ? Strings.Common_Forbidden : Strings.Editor_NotFound);
            return false;
        }

        Fill(result.Value);
        return true;
    }

    protected override async Task<bool> SaveCoreAsync()
    {
        ClearErrors();

        // Desactivar a un usuario descarta la venta que dejó guardada: se pide confirmar (Historia 8).
        if (IsEditMode && _wasActive && !IsActive && _hasHeldSale
            && !await Dialogs.ConfirmAsync(
                Strings.UserEditor_DiscardHeldSaleTitle,
                string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.UserEditor_DiscardHeldSale, UserName.Trim()),
                Strings.UserEditor_DiscardHeldSaleConfirm))
        {
            return false;
        }

        var role = SelectedRole.Role;
        var (completed, result) = await _runner.RunAsync(
            "GuardarUsuario",
            () => _userId is { } id
                ? SaveUpdateAsync(id, role)
                : SaveCreateAsync(role),
            new Dictionary<string, object?> { ["TargetUserId"] = _userId, ["UserName"] = UserName });

        if (!completed || result is null)
        {
            return false;
        }

        if (result.Error is null)
        {
            OnSaved(_userId ?? result.Value);
            return true;
        }

        await ShowErrorAsync(result.Error);
        return false;
    }

    /// <summary>Valores como se guardarían: escribir el usuario en otra capitalización no cuenta como cambio.</summary>
    protected override object CaptureState() => new UserFormState(
        FullName.Trim(),
        UserName.Trim(),
        SelectedRole.Role,
        IsActive,
        Password,
        ConfirmPassword);

    private Task<Result<Guid>> SaveCreateAsync(UserRole role)
    {
        var command = new CreateUserCommand(FullName, UserName, role, IsActive, Password, ConfirmPassword);
        return _useCases.RunAsync<CreateUserHandler, Result<Guid>>(h => h.HandleAsync(command, CancellationToken.None));
    }

    private async Task<Result<Guid>> SaveUpdateAsync(Guid id, UserRole role)
    {
        var command = new UpdateUserCommand(id, _expectedVersion, FullName, UserName, role, IsActive);
        var result = await _useCases.RunAsync<UpdateUserHandler, Result>(h => h.HandleAsync(command, CancellationToken.None));
        return result.IsSuccess ? Result.Success(id) : Result.Failure<Guid>(result.Error);
    }

    private async Task ShowErrorAsync(Error error)
    {
        switch (error)
        {
            case ValidationFailed validation:
                foreach (var field in validation.Errors)
                {
                    SetError(field.Field, field.Message);
                }

                FocusField = validation.Errors.Count > 0 ? validation.Errors[0].Field : null;
                break;

            case Duplicate:
                UserNameError = Strings.UserEditor_DuplicateUserName;
                FocusField = UserFields.UserName;
                break;

            case LastAdministrator:
                ErrorMessage = Strings.UserEditor_LastAdministrator;
                break;

            case Forbidden:
                ErrorMessage = Strings.Common_Forbidden;
                break;

            case Conflict when _userId is { } id:
                if (await Dialogs.ConfirmAsync(Strings.Editor_ConflictTitle, Strings.Editor_Conflict, Strings.Editor_Reload))
                {
                    await LoadAsync(id);
                }

                break;

            case NotFound:
                await Dialogs.ShowMessageAsync(Strings.Common_InfoTitle, Strings.Editor_NotFound);
                RaiseClosed();
                break;

            default:
                await Dialogs.ShowMessageAsync(Strings.Common_ErrorTitle, Strings.Common_UnexpectedError);
                break;
        }
    }

    private void Fill(UserDto user)
    {
        _userId = user.Id;
        _expectedVersion = user.Version;
        _wasActive = user.IsActive;
        _hasHeldSale = user.HasHeldSale;
        IsEditMode = true;
        IsSelf = _session.User?.Id == user.Id;
        FullName = user.FullName;
        UserName = user.UserName;
        SelectedRole = Roles.First(r => r.Role == user.Role);
        IsActive = user.IsActive;
        Password = ConfirmPassword = string.Empty;
        ClearErrors();
        ResetOriginalState();
    }

    private void SetError(string field, string message)
    {
        switch (field)
        {
            case UserFields.FullName:
                FullNameError = message;
                break;
            case UserFields.UserName:
                UserNameError = message;
                break;
            case UserFields.Role:
                RoleError = message;
                break;
            case UserFields.ConfirmPassword:
                ConfirmPasswordError = message;
                break;
            case UserFields.Password:
                PasswordError = message;
                break;
        }
    }

    private void ClearErrors()
    {
        FullNameError = UserNameError = RoleError = PasswordError = ConfirmPasswordError = ErrorMessage = null;
        FocusField = null;
    }
}

/// <summary>Estado normalizado del formulario de usuario, para detectar cambios.</summary>
internal sealed record UserFormState(
    string FullName,
    string UserName,
    UserRole Role,
    bool IsActive,
    string Password,
    string ConfirmPassword);
