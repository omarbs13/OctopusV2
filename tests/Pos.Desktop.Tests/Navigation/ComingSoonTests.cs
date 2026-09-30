using Pos.Desktop.Inventory;
using Pos.Desktop.Navigation;
using Pos.Desktop.Resources;
using Pos.Desktop.Tests.Home;

namespace Pos.Desktop.Tests.Navigation;

public sealed class ComingSoonTests
{
    [Fact]
    public async Task OpcionSinModulo_MuestraDisponibleMasAdelante()
    {
        using var host = HomeTestSupport.CreateHostWithModules(s =>
            s.AddComingSoonPage("test.pronto", "Pronto", "Icon.Home", 99));
        var navigator = host.Get<Navigator>();

        Assert.True(await navigator.NavigateAsync("test.pronto"));

        var page = Assert.IsType<ComingSoonViewModel>(navigator.CurrentPage, exactMatch: false);
        Assert.Equal("Pronto", page.Title);
        Assert.Equal(Strings.ComingSoon_Message, page.Message);
    }

    [Fact]
    public async Task OpcionesDeInventario_YaSonPantallasReales()
    {
        using var host = HomeTestSupport.CreateHostWithModules();
        var navigator = host.Get<Navigator>();

        Assert.True(await navigator.NavigateAsync(InventoryModule.StockPageId));
        Assert.IsType<StockViewModel>(navigator.CurrentPage);

        Assert.True(await navigator.NavigateAsync(InventoryModule.MovementsPageId));
        Assert.IsType<MovementsViewModel>(navigator.CurrentPage);
    }
}
