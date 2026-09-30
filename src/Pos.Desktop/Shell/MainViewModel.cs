using Pos.Application.Abstractions;
using Pos.Desktop.Common;
using Pos.Desktop.Navigation;
using Pos.Desktop.Resources;

namespace Pos.Desktop.Shell;

public sealed partial class MainViewModel : ViewModelBase
{
    public const string HomeEntryId = "home";

    public MainViewModel(Navigator navigator, NavigationRegistry registry, MenuViewModel menu, IAppInfo appInfo)
    {
        Menu = menu;
        ArgumentNullException.ThrowIfNull(navigator);
        ArgumentNullException.ThrowIfNull(appInfo);
        Navigator = navigator;
        Registry = registry;
        WindowTitle = $"{Strings.AppTitle} {appInfo.Version}";
        Navigator.CurrentChanged += (_, _) => OnPropertyChanged(nameof(CurrentPage));
    }

    public string WindowTitle { get; }

    public Navigator Navigator { get; }

    public NavigationRegistry Registry { get; }

    public MenuViewModel Menu { get; }

    public PageViewModel? CurrentPage => Navigator.CurrentPage;

    /// <summary>Primera pantalla al abrir la ventana principal: Inicio, o la primera registrada.</summary>
    public Task<bool> StartAsync()
    {
        var first = Registry.FindEntry(HomeEntryId) ?? (Registry.Entries.Count > 0 ? Registry.Entries[0] : null);
        return first is null ? Task.FromResult(false) : Navigator.NavigateAsync(first.Id);
    }

    /// <summary>La ventana se puede cerrar si la pantalla actual lo permite (cambios sin guardar).</summary>
    public Task<bool> CanCloseAsync() => Navigator.CanLeaveCurrentAsync();
}
