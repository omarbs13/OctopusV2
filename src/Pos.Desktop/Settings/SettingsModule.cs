using Microsoft.Extensions.DependencyInjection;
using Pos.Desktop.Navigation;
using Pos.Desktop.Resources;
using Pos.Domain.Users;

namespace Pos.Desktop.Settings;

public static class SettingsModule
{
    public const string GroupId = "settings";
    public const string BusinessPageId = "settings.business";
    public const string PrinterPageId = "settings.printer";
    public const string SecurityPageId = "settings.security";

    /// <summary>Configuración: datos del negocio, impresora, seguridad y el diálogo de apertura del cajón.</summary>
    public static IServiceCollection AddSettingsModule(this IServiceCollection services)
    {
        services.AddNavigationGroup(GroupId, Strings.Nav_Settings, "Icon.Catalog", 80);
        services.AddPage<BusinessProfileViewModel, BusinessProfileView>(BusinessPageId, Strings.Nav_BusinessProfile, "Icon.Info", 0, GroupId, permission: Permission.ManageSettings);
        services.AddPage<PrinterSettingsViewModel, PrinterSettingsView>(PrinterPageId, Strings.Nav_Printer, "Icon.Inventory", 10, GroupId, permission: Permission.ManageSettings);
        services.AddPage<SecuritySettingsViewModel, SecuritySettingsView>(SecurityPageId, Strings.Nav_Security, "Icon.Lock", 20, GroupId, permission: Permission.ManageSettings);
        services.AddComponentView<DrawerReasonViewModel, DrawerReasonView>();
        services.AddSingleton<TicketPrintingService>();
        return services;
    }
}
