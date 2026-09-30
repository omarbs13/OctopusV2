using Microsoft.Extensions.DependencyInjection;
using Pos.Desktop.Navigation;
using Pos.Desktop.Resources;

namespace Pos.Desktop.Settings;

public static class SettingsModule
{
    public const string GroupId = "settings";
    public const string BusinessPageId = "settings.business";
    public const string PrinterPageId = "settings.printer";

    /// <summary>Configuración: datos del negocio, impresora y el diálogo de apertura del cajón.</summary>
    public static IServiceCollection AddSettingsModule(this IServiceCollection services)
    {
        services.AddNavigationGroup(GroupId, Strings.Nav_Settings, "Icon.Catalog", 80);
        services.AddPage<BusinessProfileViewModel, BusinessProfileView>(BusinessPageId, Strings.Nav_BusinessProfile, "Icon.Info", 0, GroupId);
        services.AddPage<PrinterSettingsViewModel, PrinterSettingsView>(PrinterPageId, Strings.Nav_Printer, "Icon.Inventory", 10, GroupId);
        services.AddComponentView<DrawerReasonViewModel, DrawerReasonView>();
        services.AddSingleton<TicketPrintingService>();
        return services;
    }
}
