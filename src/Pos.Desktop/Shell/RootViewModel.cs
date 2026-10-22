using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Licensing;
using Pos.Application.Products.MigrationSummary;
using Pos.Application.Security;
using Pos.Application.Security.GetSecuritySettings;
using Pos.Application.Users;
using Pos.Application.Users.EndSession;
using Pos.Application.Users.GetSetupState;
using Pos.Application.Users.StartSession;
using Pos.Desktop.Auth;
using Pos.Desktop.Common;
using Pos.Desktop.Inventory;
using Pos.Desktop.Products;
using Pos.Desktop.Resources;
using Pos.Domain.Users;
using Serilog;

namespace Pos.Desktop.Shell;

/// <summary>
/// Raíz de la ventana principal (007, research §12): alterna entre el asistente de primer
/// administrador, el inicio de sesión, el cambio obligatorio de contraseña y el contenido de la
/// sesión, y muestra la capa de bloqueo por inactividad. Sin sesión no hay menú ni atajos (SC-001).
/// </summary>
public sealed partial class RootViewModel : ViewModelBase, ISessionActions, ISessionNavigation
{
    private readonly IServiceProvider _root;
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly IDialogService _dialogs;
    private readonly IDialogVisibility? _dialogVisibility;
    private readonly IdleMonitor _idle;
    private readonly ILogger _logger;

    private SessionScope? _scope;
    private double _windowWidth = double.NaN;
    private bool _leaving;

    public RootViewModel(
        IServiceProvider root,
        UseCases useCases,
        OperationRunner runner,
        IDialogService dialogs,
        IAppInfo appInfo,
        IdleMonitor idle,
        ILogger logger,
        IDialogVisibility? dialogVisibility = null)
    {
        ArgumentNullException.ThrowIfNull(appInfo);
        _root = root;
        _useCases = useCases;
        _runner = runner;
        _dialogs = dialogs;
        _dialogVisibility = dialogVisibility;
        _idle = idle;
        _logger = logger;
        WindowTitle = $"{Strings.AppTitle} {appInfo.Version}";
        _idle.IdleElapsed += (_, _) => Lock();
    }

    public string WindowTitle { get; }

    /// <summary>Pantalla actual de la ventana: asistente, inicio de sesión, cambio de contraseña o la sesión.</summary>
    [ObservableProperty]
    public partial object? Content { get; private set; }

    /// <summary>Capa de bloqueo por inactividad, sobre el contenido de la sesión.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLocked))]
    public partial LockViewModel? LockOverlay { get; private set; }

    public bool IsLocked => LockOverlay is not null;

    /// <summary>Contenido de la sesión abierta, o nulo.</summary>
    public MainViewModel? Shell => _scope?.Main;

    /// <summary>Después de la pantalla de carga: asistente si no hay usuarios; inicio de sesión si los hay.</summary>
    public async Task StartAsync()
    {
        var (completed, result) = await _runner.RunAsync(
            "ObtenerEstadoInicial",
            () => _useCases.RunAsync<GetSetupStateHandler, Result<SetupState>>(h => h.HandleAsync(CancellationToken.None)));

        if (completed && result is { IsSuccess: true } && result.Value.NeedsFirstAdmin)
        {
            ShowFirstAdmin();
            return;
        }

        ShowLogin(null);
    }

    /// <summary>Informa el ancho de la ventana para contraer el menú en ventanas angostas (FR-018).</summary>
    public void SetWindowWidth(double width)
    {
        _windowWidth = width;
        Shell?.Menu.SetWindowWidth(width);
    }

    /// <summary>Cambia la pantalla de la sesión actual; falso si no hay sesión o el destino no está disponible.</summary>
    public Task<bool> NavigateAsync(string entryId) =>
        Shell is { } shell && !IsLocked ? shell.Navigator.NavigateAsync(entryId) : Task.FromResult(false);

    public Task<bool> LogoutAsync() => LeaveSessionAsync();

    public Task<bool> SwitchUserAsync() => LeaveSessionAsync();

    /// <summary>
    /// La ventana se puede cerrar si la pantalla actual lo permite (cambios sin guardar) y el borrador
    /// de la venta terminó de guardarse. Después se cierra la sesión con <see cref="EndSessionOnCloseAsync"/>.
    /// </summary>
    public async Task<bool> CanCloseAsync()
    {
        if (Shell is not { } shell)
        {
            return true;
        }

        if (!IsLocked && !await shell.CanCloseAsync())
        {
            return false;
        }

        await shell.FlushDraftAsync();
        return true;
    }

    /// <summary>Audita el cierre de sesión al cerrar la ventana; la venta ya está en el borrador del usuario.</summary>
    public async Task EndSessionOnCloseAsync()
    {
        if (_scope is null)
        {
            return;
        }

        _idle.Stop();
        await EndSessionAsync();
    }

    [RelayCommand]
    private void ToggleMenu()
    {
        if (Shell is { } shell && !IsLocked)
        {
            shell.Menu.ToggleCommand.Execute(null);
        }
    }

    [RelayCommand]
    private void OpenPointOfSale()
    {
        if (Shell is { } shell && !IsLocked)
        {
            shell.OpenPointOfSaleCommand.Execute(null);
        }
    }

    private void ShowFirstAdmin() =>
        Content = new FirstAdminViewModel(
            _useCases,
            _runner,
            _dialogs,
            created: userName =>
            {
                ShowLogin(userName);
                return Task.CompletedTask;
            },
            alreadyDone: () =>
            {
                ShowLogin(null);
                return Task.CompletedTask;
            });

    private void ShowLogin(string? userName) =>
        Content = new LoginViewModel(_useCases, _runner, OnSignedInAsync, userName);

    private async Task OnSignedInAsync(SignInOutcome outcome)
    {
        if (outcome.MustChangePassword)
        {
            // Sin ámbito de sesión hasta completarlo: interrumpirlo no da acceso (research §15).
            Content = new ChangePasswordViewModel(
                _useCases,
                _runner,
                outcome.User.Id,
                completed: () => OpenSessionAsync(outcome.User),
                cancelled: async () =>
                {
                    await EndSessionAsync();
                    ShowLogin(null);
                });
            return;
        }

        await OpenSessionAsync(outcome.User);
    }

    private async Task OpenSessionAsync(SessionUser user)
    {
        var (completed, result) = await _runner.RunAsync(
            "AbrirSesion",
            () => _useCases.RunAsync<StartSessionHandler, Result>(h => Task.FromResult(h.Handle(user))),
            new Dictionary<string, object?> { ["SessionUserId"] = user.Id });
        if (!completed || result is not { IsSuccess: true })
        {
            ShowLogin(null);
            return;
        }

        // 025, FR-034: el estado de la licencia se recalcula al iniciar sesión (y la última fecha vista avanza).
        if (_root.GetService(typeof(LicenseBootstrapper)) is LicenseBootstrapper license)
        {
            await license.TouchAsync();
        }

        var scope = new SessionScope(_root);
        if (!double.IsNaN(_windowWidth))
        {
            scope.Main.Menu.SetWindowWidth(_windowWidth);
        }

        _scope = scope;
        await scope.Main.StartAsync();
        Content = scope.Main;

        // Alertas de existencia (022): primera revisión ya con la ventana principal; se detiene con el ámbito.
        scope.GetRequiredService<StockAlertMonitor>().Start();
        await ConfigureIdleLockAsync();
        await ShowProductMigrationSummaryAsync(scope);
    }

    /// <summary>
    /// 026: tras actualizar desde una versión anterior, el primer inicio de sesión muestra una sola vez el resumen
    /// de la migración de productos, con acceso directo a los que quedaron "Para revisión".
    /// </summary>
    private async Task ShowProductMigrationSummaryAsync(SessionScope scope)
    {
        var (completed, result) = await _runner.RunQuietlyResultAsync(
            "CargarResumenMigracionProductos",
            () => _useCases.RunAsync<GetMigrationSummaryHandler, Result<MigrationSummaryDto?>>(
                h => h.HandleAsync(CancellationToken.None)));
        if (!completed || result is not { IsSuccess: true, Value: { } summary })
        {
            return;
        }

        var message = string.Format(
            CultureInfo.CurrentCulture,
            Strings.MigrationSummary_Message,
            summary.Migrated,
            summary.WithoutModel,
            summary.WithoutPackaging,
            summary.Duplicates);
        var canView = scope.GetRequiredService<ICurrentPermissions>().Has(Permission.ViewProducts);
        var openProducts = canView
            ? await _dialogs.AskAsync(
                Strings.MigrationSummary_Title, message, Strings.MigrationSummary_ViewProducts, Strings.MigrationSummary_Close)
            : await ShowAndReturnFalseAsync(message);

        await _runner.RunQuietlyResultAsync(
            "MarcarResumenMigracionVisto",
            () => _useCases.RunAsync<MarkMigrationSummaryShownHandler, Result>(
                h => h.HandleAsync(CancellationToken.None)));

        if (openProducts)
        {
            await scope.Main.Navigator.NavigateAsync(ProductsModule.PageId, new ProductsReviewFilter());
        }
    }

    private async Task<bool> ShowAndReturnFalseAsync(string message)
    {
        await _dialogs.ShowMessageAsync(Strings.MigrationSummary_Title, message);
        return false;
    }

    /// <summary>Lee el tiempo de inactividad configurado (15 minutos por defecto; 0 lo desactiva).</summary>
    private async Task ConfigureIdleLockAsync()
    {
        var (completed, settings) = await _runner.RunQuietlyResultAsync(
            "CargarConfiguracionDeSeguridad",
            () => _useCases.RunAsync<GetSecuritySettingsHandler, Result<SecuritySettings>>(h => Task.FromResult(h.Handle())));
        _idle.Start(completed && settings is { IsSuccess: true } ? settings.Value.IdleLockMinutes : SecuritySettings.DefaultIdleLockMinutes);
    }

    /// <summary>
    /// Cerrar sesión o cambiar de usuario: pide confirmar si hay una venta en curso, espera a que se
    /// guarde, audita <c>LOGOUT</c>, desecha el ámbito y vuelve al inicio de sesión (Historia 8).
    /// </summary>
    private async Task<bool> LeaveSessionAsync()
    {
        if (_scope is not { } scope || _leaving)
        {
            return false;
        }

        _leaving = true;
        try
        {
            var shell = scope.Main;
            if (!IsLocked && !await shell.CanCloseAsync())
            {
                return false;
            }

            if (shell.HasSaleInProgress
                && !await _dialogs.AskAsync(Strings.Session_HeldSaleTitle, Strings.Session_HeldSaleConfirm, Strings.Common_Yes, Strings.Common_No))
            {
                return false;
            }

            await shell.FlushDraftAsync();
            _idle.Stop();
            await EndSessionAsync();

            LockOverlay = null;
            _scope = null;
            Content = null;
            await scope.DisposeAsync();
            ShowLogin(null);
            return true;
        }
        finally
        {
            _leaving = false;
        }
    }

    private Task<bool> EndSessionAsync() =>
        _runner.RunQuietlyAsync(
            "CerrarSesion",
            () => _useCases.RunAsync<EndSessionHandler, Result>(h => h.HandleAsync(CancellationToken.None)));

    private void Lock()
    {
        if (_scope is null || IsLocked)
        {
            return;
        }

        _logger.Information("Sesión bloqueada por inactividad");
        _dialogVisibility?.HideAll();
        var name = _scope.Main.Menu.UserSection?.DisplayName ?? string.Empty;
        LockOverlay = new LockViewModel(
            _useCases,
            _runner,
            name,
            unlocked: () =>
            {
                LockOverlay = null;
                _dialogVisibility?.ShowAll();
                _idle.Resume();
                return Task.CompletedTask;
            },
            switchUser: LeaveSessionAsync);
    }
}
