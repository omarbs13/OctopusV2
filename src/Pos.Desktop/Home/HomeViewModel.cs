using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Licensing;
using Pos.Desktop.Common;
using Pos.Desktop.Navigation;
using Pos.Desktop.Resources;
using Pos.Domain.Licensing;

namespace Pos.Desktop.Home;

/// <summary>Pantalla de inicio: tarjetas registradas por los módulos (FR-005 a FR-011).</summary>
public sealed partial class HomeViewModel : PageViewModel
{
    private readonly Navigator _navigator;
    private readonly ILicenseState? _license;

    public HomeViewModel(
        IEnumerable<DashboardCard> cards,
        Navigator navigator,
        ICurrentPermissions? permissions = null,
        ILicenseState? license = null)
    {
        _navigator = navigator;
        _license = license;

        // Las tarjetas con permiso solo se muestran a quien lo tiene (los totales del negocio no son del cajero).
        Cards = [.. cards
            .Where(c => c.RequiredPermission is not { } required || permissions is null || permissions.Has(required))
            .OrderBy(c => c.Order)];
        Metrics = [];
        Charts = [];
        ApplyLicense();
    }

    public override string Title => Strings.Home_Title;

    public IReadOnlyList<DashboardCard> Cards { get; }

    /// <summary>Tarjetas visibles: las de módulos sin licencia se ocultan sin error (012, contrato de interfaz).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<DashboardCard> Metrics { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<DashboardCard> Charts { get; private set; }

    /// <summary>Los indicadores se recalculan cada vez que se muestra Inicio (FR-010).</summary>
    public override Task OnActivatedAsync()
    {
        ApplyLicense();
        return Task.WhenAll(Metrics.Concat(Charts).Select(c => c.LoadAsync()));
    }

    private void ApplyLicense()
    {
        var visible = Cards
            .Where(c => c.RequiredPermission is not { } required
                || ModuleAccess.Required(required) is not { } module
                || _license?.IsModuleActive(module) != false)
            .ToList();
        Metrics = [.. visible.Where(c => c.Kind == DashboardCardKind.Metric)];
        Charts = [.. visible.Where(c => c.Kind == DashboardCardKind.Chart)];
    }

    [RelayCommand]
    private async Task ActivateCardAsync(DashboardCard? card)
    {
        if (card is { IsNavigable: true, NavigateTo: { } target })
        {
            await _navigator.NavigateAsync(target, card.NavigationArgument);
        }
    }
}
