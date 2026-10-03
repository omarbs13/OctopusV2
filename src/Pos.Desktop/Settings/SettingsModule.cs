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
    public const string ScannerTestPageId = "settings.scanner-test";

    /// <summary>
    /// Configuración: datos del negocio, impresora, seguridad, "Probar escáner" (sin permiso: visible
    /// para todos los roles, 023 FR-031) y el diálogo de apertura del cajón.
    /// </summary>
    public static IServiceCollection AddSettingsModule(this IServiceCollection services)
    {
        services.AddNavigationGroup(GroupId, Strings.Nav_Settings, "Icon.Cog", 80);
        services.AddPage<BusinessProfileViewModel, BusinessProfileView>(BusinessPageId, Strings.Nav_BusinessProfile, "Icon.Store", 0, GroupId, permission: Permission.ManageSettings);
        services.AddPage<PrinterSettingsViewModel, PrinterSettingsView>(PrinterPageId, Strings.Nav_Printer, "Icon.Printer", 10, GroupId, permission: Permission.ManageSettings);
        services.AddPage<SecuritySettingsViewModel, SecuritySettingsView>(SecurityPageId, Strings.Nav_Security, "Icon.Lock", 20, GroupId, permission: Permission.ManageSettings);
        services.AddPage<ScannerTestViewModel, ScannerTestView>(ScannerTestPageId, Strings.Nav_ScannerTest, "Icon.BarcodeScan", 30, GroupId);
        services.AddComponentView<DrawerReasonViewModel, DrawerReasonView>();
        services.AddSingleton<TicketPrintingService>();
        return services;
    }
}
