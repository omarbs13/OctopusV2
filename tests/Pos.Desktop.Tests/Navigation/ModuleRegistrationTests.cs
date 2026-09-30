using Pos.Desktop.Common;
using Pos.Desktop.Home;
using Pos.Desktop.Navigation;
using Pos.Desktop.Tests.Home;

namespace Pos.Desktop.Tests.Navigation;

/// <summary>
/// SC-007: un módulo nuevo agrega su opción de menú y su tarjeta de inicio solo registrándolas,
/// sin modificar vistas ni ViewModels existentes.
/// </summary>
public sealed class ModuleRegistrationTests
{
    [Fact]
    public async Task ModuloDePrueba_ApareceEnElMenuYEnInicio()
    {
        using var host = HomeTestSupport.CreateHostWithModules(services =>
        {
            services.AddNavigationGroup("test", "Módulo de prueba", "Icon.Chart", 50);
            services.AddPage<TestPageViewModel, TestPageView>("test.page", "Pantalla de prueba", "Icon.Chart", 0, "test");
            services.AddDashboardCard<TestCard>();
        });

        var registry = host.Get<NavigationRegistry>();
        var group = Assert.Single(registry.Roots, r => r.Id == "test");
        Assert.Equal(["test.page"], group.Children.Select(c => c.Id));

        var home = host.Get<HomeViewModel>();
        await home.OnActivatedAsync();
        var card = Assert.Single(home.Cards.OfType<TestCard>());
        Assert.Equal("42", card.Value);

        Assert.True(await host.Get<Navigator>().NavigateAsync("test.page"));
    }

    [Fact]
    public void ModulosReales_FormanElMenuEsperado()
    {
        using var host = HomeTestSupport.CreateHostWithModules();

        var roots = host.Get<NavigationRegistry>().Roots;

        Assert.Equal(["home", "catalogs", "inventory", "help"], roots.Select(r => r.Id));
        Assert.Equal(["catalogs.products"], roots[1].Children.Select(c => c.Id));
        Assert.Equal(["inventory.stock", "inventory.movements"], roots[2].Children.Select(c => c.Id));
        Assert.Equal(["help.about"], roots[3].Children.Select(c => c.Id));
    }

    public sealed class TestCard(OperationRunner runner) : DashboardCard(runner)
    {
        public override string Title => "Tarjeta de prueba";

        public override string Icon => "Icon.Chart";

        public override int Order => 99;

        public override DashboardCardKind Kind => DashboardCardKind.Metric;

        protected override Task LoadCoreAsync()
        {
            SetReady("42");
            return Task.CompletedTask;
        }
    }
}
