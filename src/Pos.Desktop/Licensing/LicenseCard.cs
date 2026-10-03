using Pos.Application.Licensing;
using Pos.Application.Licensing.GetLicenseStatus;
using Pos.Desktop.Common;
using Pos.Desktop.Home;
using Pos.Desktop.Resources;
using Pos.Domain.Licensing;

namespace Pos.Desktop.Licensing;

/// <summary>
/// Tarjeta de licencia de Inicio (025): estado general; en bloqueo, el mensaje de activación con sus pasos
/// (FR-029); los avisos de prueba de 5 y 1 día (FR-024), de módulos por vencer (FR-035) y de reloj atrasado
/// (FR-038). La licencia guardada rechazada se comunica con el bloqueo <c>LicenseInvalid</c>, sin aviso aparte.
/// </summary>
public sealed class LicenseCard(OperationRunner runner, UseCases useCases, LicenseBootstrapper bootstrapper) : DashboardCard(runner)
{
    public override string Title => Strings.License_CardTitle;

    public override string Icon => "Icon.Info";

    public override int Order => 0;

    public override DashboardCardKind Kind => DashboardCardKind.Metric;

    public override bool IsTextValue => true;

    protected override async Task LoadCoreAsync()
    {
        var status = await useCases.RunAsync<GetLicenseStatusHandler, LicenseStatusDto>(h => Task.FromResult(h.Handle()));

        var notes = new List<string>();
        if (status.IsBlocked)
        {
            notes.Add(LicenseMessages.Blocked(status));
            notes.Add(LicenseMessages.Contact(status));
        }

        if (status.ClockBehind)
        {
            notes.Add(LicenseMessages.ClockBehind(status));
        }

        if (bootstrapper.FileWasRegenerated)
        {
            notes.Add(Strings.License_Regenerated);
        }

        // Solo con exactamente 5 y 1 día restantes; con cualquier otro valor no hay aviso.
        switch (status.TrialWarning)
        {
            case LicenseWarning.Near:
                notes.Insert(0, Strings.License_NearExpiry);
                break;
            case LicenseWarning.Urgent:
                notes.Insert(0, Strings.License_NearExpiryOne);
                break;
        }

        notes.AddRange(status.ExpiringSoon.Select(LicenseMessages.Expiring));
        if (status.Overall == LicenseOverall.Trial)
        {
            notes.Add(Strings.License_AllModules);
        }

        SetReady(LicenseMessages.Overall(status), string.Join(" ", notes));
    }
}
