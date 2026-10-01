using System.Globalization;
using Pos.Application.Abstractions;
using Pos.Application.Licensing.GetLicenseStatus;
using Pos.Desktop.Resources;
using Pos.Domain.Licensing;

namespace Pos.Desktop.Licensing;

/// <summary>Textos de licencia vencida con el contacto del proveedor (011, FR-007).</summary>
internal static class LicenseMessages
{
    public static string Expired(LicenseExpired error) =>
        string.Format(CultureInfo.CurrentCulture, Strings.License_Expired, error.ContactPhone, error.ContactEmail);

    public static string Rejection(LicenseImportRejection reason) => reason switch
    {
        LicenseImportRejection.BadSignature => Strings.License_Reject_BadSignature,
        LicenseImportRejection.OtherMachine => Strings.License_Reject_OtherMachine,
        LicenseImportRejection.Older => Strings.License_Reject_Older,
        _ => Strings.License_Reject_Unreadable,
    };

    /// <summary>Resumen de una línea del estado vigente para Administración de licencia.</summary>
    public static string Summary(LicenseStatusDto status)
    {
        var contact = string.Format(CultureInfo.CurrentCulture, Strings.License_Contact, status.ContactPhone, status.ContactEmail);
        return status switch
        {
            { InvalidReason: { } reason } => $"{LicenseCard.ReasonText(reason)} {Strings.License_ReadOnly} {contact}",
            { IsReadOnly: true } => $"{Strings.License_ReadOnly} {contact}",
            { DaysRemaining: null } => Strings.License_SummaryUnlimited,
            { Kind: LicenseKind.Licensed, DaysRemaining: { } days } => string.Format(CultureInfo.CurrentCulture, Strings.License_SummaryLicensed, DaysText(days)),
            { DaysRemaining: { } days } => string.Format(CultureInfo.CurrentCulture, Strings.License_SummaryTrial, DaysText(days)),
            _ => string.Empty,
        };
    }

    private static string DaysText(int days) => days == 1 ? "1 día" : string.Format(CultureInfo.CurrentCulture, "{0} días", days);
}
