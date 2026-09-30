using Microsoft.Extensions.DependencyInjection;
using Pos.Desktop.Common;
using Pos.Desktop.Navigation;
using Pos.Desktop.Shell;

namespace Pos.Desktop.Auth;

public static class AuthModule
{
    /// <summary>
    /// Sesión, inicio de sesión y permisos (007): la raíz de la ventana, el monitor de inactividad y
    /// los servicios de la sesión (menú de usuario, diálogos y autorización de administrador).
    /// </summary>
    public static IServiceCollection AddAuthModule(this IServiceCollection services)
    {
        services.AddSingleton<ICurrentPermissions, SessionPermissions>();
        services.AddSingleton<IdleMonitor>();
        services.AddSingleton<RootViewModel>();
        services.AddSingleton<ISessionActions>(sp => sp.GetRequiredService<RootViewModel>());
        services.AddSingleton<ISessionNavigation>(sp => sp.GetRequiredService<RootViewModel>());
        services.AddSingleton<IDialogVisibility>(sp => (IDialogVisibility)sp.GetRequiredService<IDialogService>());

        // De la sesión: se desechan al cerrarla.
        services.AddScoped<MainViewModel>();
        services.AddScoped<ModalHost>();
        services.AddScoped<UserSectionViewModel>();
        services.AddScoped<AdminAuthorizationService>();

        services.AddComponentView<MainViewModel, MainView>();
        services.AddComponentView<FirstAdminViewModel, FirstAdminView>();
        services.AddComponentView<LoginViewModel, LoginView>();
        services.AddComponentView<ChangePasswordViewModel, ChangePasswordView>();
        services.AddComponentView<AdminAuthorizationViewModel, AdminAuthorizationView>();
        services.AddComponentView<LockViewModel, LockOverlayView>();
        return services;
    }
}
