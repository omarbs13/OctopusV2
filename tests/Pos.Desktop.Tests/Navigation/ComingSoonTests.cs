using Pos.Desktop.Navigation;
using Pos.Desktop.Resources;
using Pos.Desktop.Tests.Home;

namespace Pos.Desktop.Tests.Navigation;

public sealed class ComingSoonTests
{
    [Theory]
    [InlineData("inventory.stock", "Nav_Stock")]
    [InlineData("inventory.movements", "Nav_Movements")]
    public async Task OpcionSinModulo_MuestraDisponibleMasAdelante(string entryId, string titleKey)
    {
        using var host = HomeTestSupport.CreateHostWithModules();
        var navigator = host.Get<Navigator>();

        Assert.True(await navigator.NavigateAsync(entryId));

        var page = Assert.IsType<ComingSoonViewModel>(navigator.CurrentPage, exactMatch: false);
        Assert.Equal(Strings.ResourceManager.GetString(titleKey, Strings.Culture), page.Title);
        Assert.Equal(Strings.ComingSoon_Message, page.Message);
    }
}
