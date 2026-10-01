using System.Globalization;
using Pos.Application.Licensing.GetLicenseStatus;
using Pos.Desktop.Common;
using Pos.Desktop.Home;
using Pos.Desktop.Resources;
using Pos.Domain.Licensing;

namespace Pos.Desktop.Licensing;

/// <summary>
/// "Período de evaluación" (011, FR-006): días restantes y contacto; en modo lectura indica que el
/// sistema solo consulta; con 5 días o menos agrega el aviso de vencimiento próximo (FR-013).
/// </summary>
public sealed class LicenseCard(OperationRunner runner, UseCases useCases) : DashboardCard(runner)
{
    private bool _licensed;

    public override string Title => _licensed ? Strings.License_CardTitleLicensed : Strings.License_CardTitle;

    public override string Icon => "Icon.Info";

    public override int Order => 0;

    public override DashboardCardKind Kind => DashboardCardKind.Metric;

    protected override async Task LoadCoreAsync()
    {
        var status = await useCases.RunAsync<GetLicenseStatusHandler, LicenseStatusDto>(h => Task.FromResult(h.Handle()));
        _licensed = status.Kind == LicenseKind.Licensed;
        OnPropertyChanged(nameof(Title));

        var contact = string.Format(CultureInfo.CurrentCulture, Strings.License_Contact, status.ContactPhone, status.ContactEmail);
        switch (status)
        {
            case { IsReadOnly: true, InvalidReason: { } reason }:
                SetEmpty($"{ReasonText(reason)} {Strings.License_ReadOnly} {contact}");
                break;
            case { IsReadOnly: true }:
                SetEmpty($"{Strings.License_ReadOnly} {contact}");
                break;
            case { DaysRemaining: null }:
                SetReady(Strings.License_Active);
                break;
            case { DaysRemaining: { } days, Warning: LicenseWarning.None }:
                SetReady(DaysText(days), contact);
                break;
            case { DaysRemaining: { } days }:
                SetReady(DaysText(days), $"{NearText(days)} {contact}");
                break;
        }
    }

    internal static string ReasonText(InvalidLicenseReason reason) =>
        reason == InvalidLicenseReason.OtherMachine ? Strings.License_InvalidOtherMachine : Strings.License_InvalidCorrupt;

    private static string DaysText(int days) =>
        days == 1 ? Strings.License_OneDayRemaining : string.Format(CultureInfo.CurrentCulture, Strings.License_DaysRemaining, days);

    private static string NearText(int days) =>
        days == 1 ? Strings.License_NearExpiryOne : string.Format(CultureInfo.CurrentCulture, Strings.License_NearExpiry, days);
}
