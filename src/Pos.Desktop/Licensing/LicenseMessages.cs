using System.Globalization;
using Pos.Application.Abstractions;
using Pos.Application.Licensing.GetLicenseStatus;
using Pos.Desktop.Resources;
using Pos.Domain.Licensing;

namespace Pos.Desktop.Licensing;

/// <summary>Textos de la licencia (025, contracts/blocked-mode.md §3); nunca muestran los identificadores de los módulos.</summary>
internal static class LicenseMessages
{
    /// <summary>Rechazo de importación, siempre seguido de "Se conserva la licencia actual.".</summary>
    public static string Rejection(LicenseImportRejection reason)
    {
        var text = reason switch
        {
            LicenseImportRejection.UnsupportedFormat => Strings.License_Reject_UnsupportedFormat,
            LicenseImportRejection.BadSignature => Strings.License_Reject_BadSignature,
            LicenseImportRejection.OtherMachine => Strings.License_Reject_OtherMachine,
            LicenseImportRejection.NotNewer => Strings.License_Reject_NotNewer,
            _ => Strings.License_Reject_Unreadable,
        };
        return $"{text} {Strings.License_Kept}";
    }

    /// <summary>Resultado de una importación aceptada.</summary>
    public static string Imported(LicenseStatusDto status) => status.IsBlocked
        ? Format(Strings.License_ImportedBlocked, Cause(status))
        : Strings.License_Imported;

    /// <summary>Estado general: "En prueba: N días restantes", "Licenciado" o "Bloqueado: {causa}".</summary>
    public static string Overall(LicenseStatusDto status) => status.Overall switch
    {
        LicenseOverall.Trial => Format(Strings.License_OverallTrial, DaysText(status.TrialDaysRemaining)),
        LicenseOverall.Licensed => Strings.License_OverallLicensed,
        _ => Format(Strings.License_OverallBlocked, Cause(status)),
    };

    /// <summary>Causa corta del bloqueo.</summary>
    public static string Cause(LicenseStatusDto status) => status.BlockReason switch
    {
        LicenseBlockReason.TrialExpired => Strings.License_CauseTrialExpired,
        LicenseBlockReason.LicenseInvalid => Strings.License_CauseLicenseInvalid,
        LicenseBlockReason.BaseNotLicensed => Strings.License_CauseBaseNotLicensed,
        LicenseBlockReason.BasePending => Strings.License_CauseBasePending,
        _ => Strings.License_CauseBaseExpired,
    };

    /// <summary>Mensaje de activación con los pasos, según la causa del bloqueo (FR-029).</summary>
    public static string Blocked(LicenseStatusDto status)
    {
        var pos = status.Modules.FirstOrDefault(m => m.Module == ModuleCatalog.Base);
        return status.BlockReason switch
        {
            LicenseBlockReason.TrialExpired => Strings.License_BlockedTrialExpired,
            LicenseBlockReason.LicenseInvalid => Strings.License_BlockedLicenseInvalid,
            LicenseBlockReason.BaseNotLicensed => Strings.License_BlockedBaseNotLicensed,
            LicenseBlockReason.BasePending => Format(Strings.License_BlockedBasePending, DateText(pos?.ActivatesOn)),
            _ => Format(Strings.License_BlockedBaseExpired, DateText(pos?.ExpiresOn)),
        };
    }

    public static string State(ModuleState state) => state switch
    {
        ModuleState.Active => Strings.License_StateActive,
        ModuleState.Pending => Strings.License_StatePending,
        ModuleState.Expired => Strings.License_StateExpired,
        _ => Strings.License_StateNotLicensed,
    };

    /// <summary>"{Módulo} vence el {fecha}." (Inicio, FR-035).</summary>
    public static string Expiring(ModuleStatusDto module) => Format(Strings.License_ModuleExpiring, module.Name, DateText(module.ExpiresOn));

    public static string ClockBehind(LicenseStatusDto status) => Format(Strings.License_ClockBehind, DateText(status.LastSeen));

    public static string Contact(LicenseStatusDto status) => Format(Strings.License_Contact, status.ContactPhone, status.ContactEmail);

    public static string DateText(DateOnly? date) =>
        date is { } d ? d.ToString("d", CultureInfo.CurrentCulture) : Strings.License_Indefinite;

    internal static string DaysText(int days) =>
        days == 1 ? Strings.License_OneDayRemaining : Format(Strings.License_DaysRemaining, days);

    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.CurrentCulture, format, args);
}
