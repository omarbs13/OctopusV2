using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Desktop.Common;
using Pos.Desktop.Navigation;
using Pos.Desktop.Resources;
using Pos.Desktop.Sales;

namespace Pos.Desktop.Shell;

public sealed partial class MainViewModel : ViewModelBase
{
    public const string HomeEntryId = "home";

    public MainViewModel(
        Navigator navigator,
        NavigationRegistry registry,
        MenuViewModel menu,
        IAppInfo appInfo,
        ModalHost? modal = null,
        NotificationCenter? notifications = null)
    {
        Menu = menu;
        Modal = modal ?? new ModalHost();
        ArgumentNullException.ThrowIfNull(navigator);
        Notifications = notifications ?? new NotificationCenter(navigator);
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

    /// <summary>Diálogo sobre la sesión (cambio de contraseña, autorización de administrador).</summary>
    public ModalHost Modal { get; }

    /// <summary>Notificaciones no bloqueantes de la sesión, abajo a la derecha (022).</summary>
    public NotificationCenter Notifications { get; }

    /// <summary>
    /// Hay una venta en curso en el Punto de venta de esta sesión. Solo consulta la pantalla si ya se
    /// abrió: no la crea.
    /// </summary>
    public bool HasSaleInProgress =>
        Navigator.Resolved<PointOfSaleViewModel>(SalesModule.PointOfSalePageId)?.HasLines == true;

    /// <summary>Espera a que termine el guardado del borrador antes de cerrar la sesión (Principio I).</summary>
    public Task FlushDraftAsync() =>
        Navigator.Resolved<PointOfSaleViewModel>(SalesModule.PointOfSalePageId)?.FlushDraftAsync() ?? Task.CompletedTask;

    public PageViewModel? CurrentPage => Navigator.CurrentPage;

    /// <summary>Primera pantalla al abrir la ventana principal: Inicio, o la primera registrada.</summary>
    public Task<bool> StartAsync()
    {
        var first = Registry.FindEntry(HomeEntryId) ?? (Registry.Entries.Count > 0 ? Registry.Entries[0] : null);
        return first is null ? Task.FromResult(false) : Navigator.NavigateAsync(first.Id);
    }

    /// <summary>F9: abre el Punto de venta desde cualquier pantalla.</summary>
    [RelayCommand]
    private Task<bool> OpenPointOfSaleAsync() => Navigator.NavigateAsync(SalesModule.PointOfSalePageId);

    /// <summary>La ventana se puede cerrar si la pantalla actual lo permite (cambios sin guardar).</summary>
    public Task<bool> CanCloseAsync() => Navigator.CanLeaveCurrentAsync();
}
