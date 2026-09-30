using Microsoft.Extensions.DependencyInjection;
using Pos.Desktop.Navigation;
using Pos.Desktop.Resources;
using Pos.Domain.Users;

namespace Pos.Desktop.Administration;

public static class AdministrationModule
{
    public const string GroupId = "administration";
    public const string UsersPageId = "administration.users";
    public const string AuditLogPageId = "administration.audit";

    /// <summary>Administración (antes de Configuración): usuarios y bitácora de auditoría, solo para Administrador.</summary>
    public static IServiceCollection AddAdministrationModule(this IServiceCollection services)
    {
        services.AddNavigationGroup(GroupId, Strings.Nav_Administration, "Icon.Shield", 70);
        services.AddPage<UsersViewModel, UsersView>(UsersPageId, Strings.Nav_Users, "Icon.Users", 0, GroupId, permission: Permission.ManageUsers);
        services.AddPage<AuditLogViewModel, AuditLogView>(AuditLogPageId, Strings.Nav_AuditLog, "Icon.Movements", 10, GroupId, permission: Permission.ViewAuditLog);

        services.AddTransient<UserEditorViewModel>();
        services.AddScoped<Func<UserEditorViewModel>>(sp => sp.GetRequiredService<UserEditorViewModel>);
        services.AddComponentView<UserEditorViewModel, UserEditorView>();
        services.AddComponentView<ResetPasswordViewModel, ResetPasswordView>();
        return services;
    }
}
