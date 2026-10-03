using Pos.Application.Licensing;
using Pos.Application.Licensing.GetLicenseStatus;
using Pos.Desktop.Common;
using Pos.Desktop.Home;
using Pos.Desktop.Resources;
using Pos.Domain.Licensing;

namespace Pos.Desktop.Licensing;

/// <summary>
/// Tarjeta de licencia de Inicio (012): días restantes de la evaluación con los avisos exactos de 5 y 1
/// día (FR-015, FR-016) o, en modo modular, los módulos activos. Avisa si el archivo se regeneró.
/// </summary>
public sealed class LicenseCard(OperationRunner runner, UseCases useCases, LicenseBootstrapper bootstrapper) : DashboardCard(runner)
{
    private bool _modular;

    public override string Title => _modular ? Strings.License_CardTitleModular : Strings.License_CardTitle;

    public override string Icon => "Icon.Info";

    public override int Order => 0;

    public override DashboardCardKind Kind => DashboardCardKind.Metric;

    public override bool IsTextValue => true;

    protected override async Task LoadCoreAsync()
    {
        var status = await useCases.RunAsync<GetLicenseStatusHandler, LicenseStatusDto>(h => Task.FromResult(h.Handle()));
        _modular = status.Phase == LicensePhase.Modular;
        OnPropertyChanged(nameof(Title));

        var notes = new List<string>();
        if (bootstrapper.FileWasRegenerated)
        {
            notes.Add($"{Strings.License_Regenerated} {LicenseMessages.Contact(status)}");
        }

        if (_modular)
        {
            notes.Add(string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.License_ModulesActive, LicenseMessages.ModulesText(status.ActiveModules)));
            SetReady(Strings.License_Active, string.Join(" ", notes));
            return;
        }

        // Solo con exactamente 5 y 1 día restantes (SC-006); con cualquier otro valor no hay aviso.
        switch (status.Warning)
        {
            case LicenseWarning.Near:
                notes.Insert(0, Strings.License_NearExpiry);
                break;
            case LicenseWarning.Urgent:
                notes.Insert(0, Strings.License_NearExpiryOne);
                break;
        }

        notes.Add(Strings.License_AllModules);
        SetReady(LicenseMessages.DaysText(status.DaysRemaining), string.Join(" ", notes));
    }
}
