using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Licensing;
using Pos.Domain.Licensing;
using Pos.Desktop.Common;
using Pos.Desktop.Diagnostics;
using Pos.Desktop.Resources;
using Serilog;

namespace Pos.Desktop.Navigation;

/// <summary>
/// Único punto que cambia la pantalla actual. Antes de salir consulta a la pantalla actual
/// (cambios sin guardar); las pantallas son singletons, así que conservan su estado en la sesión.
/// </summary>
public sealed class Navigator
{
    private readonly NavigationRegistry _registry;
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;
    private readonly DiagnosticContext? _diagnostics;
    private readonly ILicenseState? _license;
    private readonly IDialogService? _dialogs;
    private readonly Dictionary<string, PageViewModel> _resolved = [];

    public Navigator(
        NavigationRegistry registry,
        IServiceProvider services,
        ILogger logger,
        DiagnosticContext? diagnostics = null,
        ILicenseState? license = null,
        IDialogService? dialogs = null)
    {
        _license = license;
        _dialogs = dialogs;
        _registry = registry;
        _services = services;
        _logger = logger;
        _diagnostics = diagnostics;

        // La pantalla segura es el Punto de venta (FR-004); la sesión vigente es la última que se registra.
        _diagnostics?.SetSafeScreenAction(() => NavigateAsync(SafeScreenId));
    }

    /// <summary>Opción a la que se regresa cuando la pantalla actual no puede continuar tras un error.</summary>
    public const string SafeScreenId = Sales.SalesModule.PointOfSalePageId;

    public event EventHandler? CurrentChanged;

    public string? CurrentEntryId { get; private set; }

    public PageViewModel? CurrentPage { get; private set; }

    /// <summary>
    /// Navega a una opción. Si la pantalla destino implementa <see cref="INavigationArgumentReceiver"/>,
    /// recibe el argumento antes de <see cref="PageViewModel.OnActivatedAsync"/>, aunque ya sea la
    /// pantalla actual (research §9).
    /// </summary>
    public async Task<bool> NavigateAsync(string entryId, object? argument = null)
    {
        if (entryId == CurrentEntryId)
        {
            if (argument is not null && CurrentPage is INavigationArgumentReceiver current)
            {
                current.Receive(argument);
                await CurrentPage.OnActivatedAsync();
            }

            return true;
        }

        var entry = _registry.FindEntry(entryId);
        if (entry is null)
        {
            _logger.Warning("Se intentó navegar a la opción inexistente {EntryId}", entryId);
            return false;
        }

        // 012: el acceso directo o el atajo a un módulo sin licencia se rechaza sin modificar nada.
        if (entry.Permission is { } required && ModuleAccess.Required(required) is { } module
            && _license?.IsModuleActive(module) == false)
        {
            await ShowModuleNotLicensedAsync();
            return false;
        }

        if (!await CanLeaveCurrentAsync())
        {
            return false;
        }

        CurrentEntryId = entry.Id;
        CurrentPage = (PageViewModel)(entry.Resolve?.Invoke(_services) ?? _services.GetRequiredService(entry.ViewModelType));
        _resolved[entry.Id] = CurrentPage;
        if (argument is not null && CurrentPage is INavigationArgumentReceiver receiver)
        {
            receiver.Receive(argument);
        }

        _diagnostics?.SetScreen(CurrentEntryId);
        CurrentChanged?.Invoke(this, EventArgs.Empty);
        await CurrentPage.OnActivatedAsync();
        return true;
    }

    /// <summary>La pantalla de la opción si ya se abrió en esta sesión; nulo si nunca se ha abierto.</summary>
    public TPage? Resolved<TPage>(string entryId)
        where TPage : PageViewModel =>
        _resolved.GetValueOrDefault(entryId) as TPage;

    public Task<bool> CanLeaveCurrentAsync() =>
        CurrentPage is ILeaveGuard guard ? guard.CanLeaveAsync() : Task.FromResult(true);

    private Task ShowModuleNotLicensedAsync() =>
        _dialogs?.ShowMessageAsync(Strings.Common_InfoTitle, Strings.License_ModuleNotLicensed) ?? Task.CompletedTask;
}
