using Avalonia.Controls;
using Pos.Desktop.Navigation;

namespace Pos.Desktop.Tests.Navigation;

public class NavigationRegistryTests
{
    private static NavigationEntry Entry(string id, int order, string? group = null) =>
        new(id, id, "Icon.Home", order, group, typeof(TestPageViewModel));

    [Fact]
    public void Construye_ElArbolOrdenadoConOpcionesDePrimerNivelYGrupos()
    {
        var registry = new NavigationRegistry(
            [new NavigationGroup("help", "Ayuda", "Icon.Help", 90), new NavigationGroup("catalogs", "Catálogos", "Icon.Catalog", 10)],
            [Entry("help.about", 0, "help"), Entry("catalogs.products", 0, "catalogs"), Entry("home", 0), Entry("catalogs.other", -1, "catalogs")]);

        var roots = registry.Roots;

        Assert.Equal(["home", "catalogs", "help"], roots.Select(r => r.Id));
        Assert.Null(roots[0].Group);
        Assert.Equal("home", roots[0].Entry!.Id);
        Assert.Equal(["catalogs.other", "catalogs.products"], roots[1].Children.Select(c => c.Id));
        Assert.Equal("catalogs", registry.GroupOf("catalogs.products")!.Id);
        Assert.Null(registry.GroupOf("home"));
    }

    [Fact]
    public void GrupoSinOpciones_NoAparece()
    {
        var registry = new NavigationRegistry([new NavigationGroup("empty", "Vacío", "Icon.Help", 1)], [Entry("home", 0)]);

        Assert.Equal(["home"], registry.Roots.Select(r => r.Id));
    }

    [Fact]
    public void IdDuplicado_Lanza()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new NavigationRegistry([], [Entry("home", 0), Entry("home", 1)]));

        Assert.Contains("home", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GrupoInexistente_Lanza()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new NavigationRegistry([], [Entry("x.y", 0, "noexiste")]));

        Assert.Contains("noexiste", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalizadorDeVistas_ConstruyeLaVistaRegistradaYSoloReconoceTiposRegistrados()
    {
        var locator = new RegisteredViewLocator([new ViewRegistration(typeof(TestPageViewModel), () => new TestPageView())]);

        Assert.True(locator.Match(new TestPageViewModel()));
        Assert.False(locator.Match(new OtherTestPageViewModel()));
        Assert.False(locator.Match("texto"));
        Assert.IsType<TestPageView>(locator.Build(new TestPageViewModel()));
        Assert.IsType<TextBlock>(locator.Build(new OtherTestPageViewModel()));
    }
}
