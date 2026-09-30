using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Pos.Desktop.Common;
using Pos.Desktop.Home;

namespace Pos.Desktop.Navigation;

/// <summary>Registro de módulos: cada módulo agrega su menú, pantallas y vistas sin tocar las vistas existentes.</summary>
public static class NavigationServiceCollectionExtensions
{
    public static IServiceCollection AddNavigationCore(this IServiceCollection services)
    {
        services.AddSingleton<NavigationRegistry>();
        services.AddSingleton<Navigator>();
        services.AddSingleton<RegisteredViewLocator>();
        services.AddSingleton<MenuViewModel>();
        return services;
    }

    public static IServiceCollection AddNavigationGroup(
        this IServiceCollection services, string id, string title, string icon, int order) =>
        services.AddSingleton(new NavigationGroup(id, title, icon, order));

    /// <summary>Pantalla navegable: ViewModel singleton (conserva su estado en la sesión), opción de menú y vista.</summary>
    public static IServiceCollection AddPage<TViewModel, TView>(
        this IServiceCollection services, string id, string title, string icon, int order, string? groupId = null)
        where TViewModel : PageViewModel
        where TView : Control, new()
    {
        services.AddSingleton<TViewModel>();
        services.AddSingleton(new NavigationEntry(id, title, icon, order, groupId, typeof(TViewModel)));
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

    /// <summary>Tarjeta de Inicio de un módulo (singleton).</summary>
    public static IServiceCollection AddDashboardCard<TCard>(this IServiceCollection services)
        where TCard : DashboardCard =>
        services.AddSingleton<DashboardCard, TCard>();

    /// <summary>Solo la vista, para ViewModels que no son pantallas (por ejemplo, un formulario).</summary>
    public static IServiceCollection AddComponentView<TViewModel, TView>(this IServiceCollection services)
        where TView : Control, new() =>
        services.AddSingleton(new ViewRegistration(typeof(TViewModel), () => new TView()));
}
