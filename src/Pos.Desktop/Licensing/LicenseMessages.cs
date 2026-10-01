using System.Globalization;
using Pos.Application.Abstractions;
using Pos.Application.Licensing.GetLicenseStatus;
using Pos.Desktop.Resources;
using Pos.Domain.Licensing;

namespace Pos.Desktop.Licensing;

/// <summary>Textos de la licencia modular (012); nunca muestran los identificadores de los módulos.</summary>
internal static class LicenseMessages
{
    public static string Rejection(LicenseImportRejection reason) => reason switch
    {
        LicenseImportRejection.BadSignature => Strings.License_Reject_BadSignature,
        LicenseImportRejection.OtherMachine => Strings.License_Reject_OtherMachine,
        _ => Strings.License_Reject_Unreadable,
    };

    public static string ModuleName(LicensedModule module) => module switch
    {
        LicensedModule.Inventory => Strings.Module_Inventory,
        LicensedModule.AdvancedReports => Strings.Module_AdvancedReports,
        LicensedModule.CreditAndCustomers => Strings.Module_CreditAndCustomers,
        LicensedModule.CashShifts => Strings.Module_CashShifts,
        LicensedModule.Discounts => Strings.Module_Discounts,
        _ => Strings.Module_Returns,
    };

    /// <summary>Nombres de los módulos activos, o "ninguno".</summary>
    public static string ModulesText(IReadOnlyList<LicensedModule> modules) =>
        modules.Count == 0 ? Strings.License_NoModules : string.Join(", ", modules.Select(ModuleName));

    /// <summary>Resumen de una línea del estado vigente para Administración de licencia.</summary>
    public static string Summary(LicenseStatusDto status) => status.Phase == LicensePhase.Trial
        ? string.Format(CultureInfo.CurrentCulture, Strings.License_SummaryTrial, DaysText(status.DaysRemaining))
        : string.Format(CultureInfo.CurrentCulture, Strings.License_SummaryModular, string.Format(CultureInfo.CurrentCulture, Strings.License_ModulesActive, ModulesText(status.ActiveModules)));

    public static string Contact(LicenseStatusDto status) =>
        string.Format(CultureInfo.CurrentCulture, Strings.License_Contact, status.ContactPhone, status.ContactEmail);

    internal static string DaysText(int days) =>
        days == 1 ? Strings.License_OneDayRemaining : string.Format(CultureInfo.CurrentCulture, Strings.License_DaysRemaining, days);
}
