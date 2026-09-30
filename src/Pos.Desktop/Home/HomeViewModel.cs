using CommunityToolkit.Mvvm.Input;
using Pos.Desktop.Common;
using Pos.Desktop.Navigation;
using Pos.Desktop.Resources;

namespace Pos.Desktop.Home;

/// <summary>Pantalla de inicio: tarjetas registradas por los módulos (FR-005 a FR-011).</summary>
public sealed partial class HomeViewModel : PageViewModel
{
    private readonly Navigator _navigator;

    public HomeViewModel(IEnumerable<DashboardCard> cards, Navigator navigator, ICurrentPermissions? permissions = null)
    {
        _navigator = navigator;

        // Las tarjetas con permiso solo se muestran a quien lo tiene (los totales del negocio no son del cajero).
        Cards = [.. cards
            .Where(c => c.RequiredPermission is not { } required || permissions is null || permissions.Has(required))
            .OrderBy(c => c.Order)];
        Metrics = [.. Cards.Where(c => c.Kind == DashboardCardKind.Metric)];
        Charts = [.. Cards.Where(c => c.Kind == DashboardCardKind.Chart)];
    }

    public override string Title => Strings.Home_Title;

    public IReadOnlyList<DashboardCard> Cards { get; }

    public IReadOnlyList<DashboardCard> Metrics { get; }

    public IReadOnlyList<DashboardCard> Charts { get; }

    /// <summary>Los indicadores se recalculan cada vez que se muestra Inicio (FR-010).</summary>
    public override Task OnActivatedAsync() => Task.WhenAll(Cards.Select(c => c.LoadAsync()));

    [RelayCommand]
    private async Task ActivateCardAsync(DashboardCard? card)
    {
        if (card is { IsNavigable: true, NavigateTo: { } target })
        {
            await _navigator.NavigateAsync(target, card.NavigationArgument);
        }
    }
}
