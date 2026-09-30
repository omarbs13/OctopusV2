using Microsoft.Extensions.DependencyInjection;
using Pos.Desktop.Common;
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

    public Navigator(NavigationRegistry registry, IServiceProvider services, ILogger logger)
    {
        _registry = registry;
        _services = services;
        _logger = logger;
    }

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

        if (!await CanLeaveCurrentAsync())
        {
            return false;
        }

        CurrentEntryId = entry.Id;
        CurrentPage = (PageViewModel)(entry.Resolve?.Invoke(_services) ?? _services.GetRequiredService(entry.ViewModelType));
        if (argument is not null && CurrentPage is INavigationArgumentReceiver receiver)
        {
            receiver.Receive(argument);
        }

        CurrentChanged?.Invoke(this, EventArgs.Empty);
        await CurrentPage.OnActivatedAsync();
        return true;
    }

    public Task<bool> CanLeaveCurrentAsync() =>
        CurrentPage is ILeaveGuard guard ? guard.CanLeaveAsync() : Task.FromResult(true);
}
