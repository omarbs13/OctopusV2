using Pos.Desktop.Home;
using Pos.Desktop.Home.Cards;
using Pos.Desktop.Inventory;
using Pos.Desktop.Navigation;
using Pos.Desktop.Resources;
using Pos.Desktop.Sales;
using Pos.Desktop.Tests.TestSupport;
using Pos.Domain.Common;
using Pos.Domain.Products;
using Serilog.Events;

namespace Pos.Desktop.Tests.Home;

public sealed class HomeViewModelTests : IDisposable
{
    private readonly DesktopTestHost _host = HomeTestSupport.CreateHostWithModules();

    public void Dispose() => _host.Dispose();

    private HomeViewModel Home => _host.Get<HomeViewModel>();

    private void SeedActive(int count)
    {
        for (var i = 0; i < count; i++)
        {
            _host.Repository.Seed(Product.Create($"Producto {i}", $"P-{Guid.NewGuid():N}"[..20], null, Money.FromCents(100), "H87"));
        }
    }

    [Fact]
    public async Task AlActivarse_CargaTodasLasTarjetasOrdenadasYSeparadas()
    {
        SeedActive(12);

        await Home.OnActivatedAsync();

        Assert.Equal(
            [Strings.Card_ActiveProducts, Strings.Card_LowStock, Strings.Card_OutOfStock],
            Home.Metrics.Select(c => c.Title));
        Assert.Equal(
            [Strings.Chart_SalesToday, Strings.Chart_SalesLast7Days, Strings.Chart_TopProducts],
            Home.Charts.Select(c => c.Title));
        Assert.DoesNotContain(Home.Cards, c => c.State == DashboardCardState.Loading);
    }

    [Fact]
    public async Task ProductosActivos_MuestraElConteoReal()
    {
        SeedActive(12);
        var inactive = Product.Create("Inactivo", "INA-1", null, Money.FromCents(100), "H87");
        inactive.Update(inactive.Name, inactive.Sku, null, inactive.Price, "H87", isActive: false);
        _host.Repository.Seed(inactive);

        await Home.OnActivatedAsync();

        var card = Assert.Single(Home.Cards.OfType<ActiveProductsCard>());
        Assert.Equal(DashboardCardState.Ready, card.State);
        Assert.Equal("12", card.Value);
        Assert.True(card.IsNavigable);
    }

    [Fact]
    public async Task Ventas_SinVentas_MuestraCeroEnElDiaYEstadoVacioEnLasGraficas_EInventarioMuestraConteoNavegable()
    {
        await Home.OnActivatedAsync();

        var today = Home.Cards.OfType<SalesTodayChart>().Single();
        Assert.Equal(DashboardCardState.Ready, today.State);
        Assert.Equal("$0.00 · 0 ventas", today.Value);
        Assert.True(today.IsNavigable);

        var week = Home.Cards.OfType<SalesLast7DaysChart>().Single();
        Assert.Equal(DashboardCardState.Empty, week.State);
        Assert.Equal(Strings.Card_SalesWeekEmpty, week.Message);

        var top = Home.Cards.OfType<TopProductsChart>().Single();
        Assert.Equal(DashboardCardState.Empty, top.State);
        Assert.Equal(Strings.Card_TopProductsEmpty, top.Message);

        var inventory = Home.Cards.Where(c => c is LowStockCard or OutOfStockCard).ToList();
        Assert.Equal(2, inventory.Count);
        Assert.All(inventory, c =>
        {
            Assert.Equal(DashboardCardState.Ready, c.State);
            Assert.Equal("0", c.Value);
            Assert.True(c.IsNavigable);
        });
    }

    [Fact]
    public async Task TarjetaConError_SeAislaSeRegistraYNoMuestraDialogo()
    {
        using var host = HomeTestSupport.CreateHostWithModules(s => s.AddDashboardCard<FailingCard>());
        var home = host.Get<HomeViewModel>();

        await home.OnActivatedAsync();

        var failing = Assert.Single(home.Cards.OfType<FailingCard>());
        Assert.Equal(DashboardCardState.Error, failing.State);
        Assert.Equal(Strings.Card_Unavailable, failing.Message);
        Assert.Equal(DashboardCardState.Ready, home.Cards.OfType<ActiveProductsCard>().Single().State);
        Assert.Empty(host.Dialogs.Messages);
        var error = Assert.Single(host.Sink.Events, e => e.Level == LogEventLevel.Error);
        Assert.Equal("\"CargarTarjeta\"", error.Properties["Operation"].ToString());
        Assert.Equal("\"Tarjeta que falla\"", error.Properties["Card"].ToString());
    }

    [Fact]
    public async Task ActivarProductosActivos_NavegaAProductos_YUnaVaciaNo()
    {
        var navigator = _host.Get<Navigator>();
        await navigator.NavigateAsync("home");
        var home = (HomeViewModel)navigator.CurrentPage!;

        await home.ActivateCardCommand.ExecuteAsync(home.Cards.OfType<SalesLast7DaysChart>().Single());
        Assert.Equal("home", navigator.CurrentEntryId);

        await home.ActivateCardCommand.ExecuteAsync(home.Cards.OfType<ActiveProductsCard>().Single());
        Assert.Equal("catalogs.products", navigator.CurrentEntryId);
    }

    [Fact]
    public async Task VolverAInicio_ActualizaLosIndicadores()
    {
        SeedActive(2);
        await Home.OnActivatedAsync();

        SeedActive(1);
        await Home.OnActivatedAsync();

        Assert.Equal("3", Home.Cards.OfType<ActiveProductsCard>().Single().Value);
    }
}
