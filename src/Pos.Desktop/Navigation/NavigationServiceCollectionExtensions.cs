using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Pos.Desktop.Common;
using Pos.Desktop.Home;
using Pos.Domain.Users;

namespace Pos.Desktop.Navigation;

/// <summary>Registro de módulos: cada módulo agrega su menú, pantallas y vistas sin tocar las vistas existentes.</summary>
public static class NavigationServiceCollectionExtensions
{
    public static IServiceCollection AddNavigationCore(this IServiceCollection services)
    {
        // Todo lo que pertenece a una sesión es scoped: al cerrar sesión se desecha el ámbito y la
        // siguiente sesión empieza con pantallas limpias y el menú de su rol (007, research §12).
        services.AddScoped(sp => new NavigationRegistry(
            sp.GetServices<NavigationGroup>(),
            sp.GetServices<NavigationEntry>(),
            sp.GetService<ICurrentPermissions>() is { } permissions ? permissions.Has : null));
        services.AddScoped<Navigator>();
        services.AddSingleton<RegisteredViewLocator>();
        services.AddScoped<MenuViewModel>();
        return services;
    }

    public static IServiceCollection AddNavigationGroup(
        this IServiceCollection services, string id, string title, string icon, int order) =>
        services.AddSingleton(new NavigationGroup(id, title, icon, order));

    /// <summary>Pantalla navegable: ViewModel de la sesión (conserva su estado mientras dura), opción de menú y vista.</summary>
    public static IServiceCollection AddPage<TViewModel, TView>(
        this IServiceCollection services,
        string id,
        string title,
        string icon,
        int order,
        string? groupId = null,
        string? shortcut = null,
        Permission? permission = null)
        where TViewModel : PageViewModel
        where TView : Control, new()
    {
        services.AddScoped<TViewModel>();
        services.AddSingleton(new NavigationEntry(id, title, icon, order, groupId, typeof(TViewModel), Shortcut: shortcut, Permission: permission));
        return services.AddComponentView<TViewModel, TView>();
    }

    /// <summary>Opción de un módulo que aún no existe: muestra "disponible más adelante" (FR-020a).</summary>
    public static IServiceCollection AddComingSoonPage(
        this IServiceCollection services, string id, string title, string icon, int order, string? groupId = null)
    {
        var page = new ComingSoonViewModel(title, icon);
        services.AddSingleton(new NavigationEntry(id, title, icon, order, groupId, typeof(ComingSoonViewModel), _ => page));
        return services.AddComponentView<ComingSoonViewModel, ComingSoonView>();
    }

    /// <summary>Tarjeta de Inicio de un módulo (de la sesión).</summary>
    public static IServiceCollection AddDashboardCard<TCard>(this IServiceCollection services)
        where TCard : DashboardCard =>
        services.AddScoped<DashboardCard, TCard>();

    /// <summary>Solo la vista, para ViewModels que no son pantallas (por ejemplo, un formulario).</summary>
    public static IServiceCollection AddComponentView<TViewModel, TView>(this IServiceCollection services)
        where TView : Control, new() =>
        services.AddSingleton(new ViewRegistration(typeof(TViewModel), () => new TView()));
}
