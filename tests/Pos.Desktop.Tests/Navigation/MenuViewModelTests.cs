using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Abstractions;
using Pos.Desktop.Navigation;
using Pos.Desktop.Tests.Home;
using Pos.Desktop.Tests.TestSupport;

namespace Pos.Desktop.Tests.Navigation;

public sealed class MenuViewModelTests : IDisposable
{
    private readonly InMemoryPreferencesStore _preferences = new();
    private readonly DesktopTestHost _host;

    public MenuViewModelTests()
    {
        _host = HomeTestSupport.CreateHostWithModules(s => s.AddSingleton<IPreferencesStore>(_preferences));
    }

    public void Dispose() => _host.Dispose();

    private MenuViewModel CreateMenu() => new(_host.Get<NavigationRegistry>(), _host.Get<Navigator>(), _preferences);

    private static MenuItemViewModel Group(MenuViewModel menu, string id) => menu.Items.Single(i => i.Id == id);

    private NavigationPreferences? Saved => _preferences.Load<NavigationPreferences>(MenuViewModel.PreferencesKey);

    [Fact]
    public void EstructuraInicial()
    {
        var menu = CreateMenu();

        Assert.Equal(["home", "sales", "catalogs", "inventory", "administration", "settings", "help"], menu.Items.Select(i => i.Id));
        Assert.False(menu.Items[0].IsGroup);
        Assert.Equal(["sales.pos", "sales.history"], Group(menu, "sales").Children.Select(c => c.Id));
        Assert.Equal("F9", Group(menu, "sales").Children[0].Shortcut);
        Assert.Equal(["catalogs.products"], Group(menu, "catalogs").Children.Select(c => c.Id));
        Assert.Equal(["inventory.stock", "inventory.movements"], Group(menu, "inventory").Children.Select(c => c.Id));
        Assert.Equal(["administration.users", "administration.audit"], Group(menu, "administration").Children.Select(c => c.Id));
        Assert.Equal(["settings.business", "settings.printer", "settings.security", "settings.scanner-test"], Group(menu, "settings").Children.Select(c => c.Id));
        Assert.Equal(["help.about"], Group(menu, "help").Children.Select(c => c.Id));
    }

    [Fact]
    public void Alternar_ContraeYExpandeYGuardaLaPreferencia()
    {
        var menu = CreateMenu();

        menu.ToggleCommand.Execute(null);

        Assert.True(menu.IsCollapsed);
        Assert.True(Saved!.Collapsed);

        menu.ToggleCommand.Execute(null);

        Assert.False(menu.IsCollapsed);
        Assert.False(Saved!.Collapsed);
    }

    [Fact]
    public void AbrirYCerrarGrupo_GuardaLosGruposAbiertos()
    {
        var menu = CreateMenu();

        menu.ToggleGroupCommand.Execute(Group(menu, "inventory"));

        Assert.True(Group(menu, "inventory").IsExpanded);
        Assert.Contains("inventory", Saved!.ExpandedGroups);

        menu.ToggleGroupCommand.Execute(Group(menu, "inventory"));

        Assert.DoesNotContain("inventory", Saved!.ExpandedGroups);
    }

    [Fact]
    public void PreferenciaGuardada_SeRestaura_IgnorandoGruposInexistentes()
    {
        _preferences.Save(MenuViewModel.PreferencesKey, new NavigationPreferences(true, ["inventory", "yanoexiste"]));

        var menu = CreateMenu();

        Assert.True(menu.IsCollapsed);
        Assert.True(Group(menu, "inventory").IsExpanded);
        Assert.False(Group(menu, "catalogs").IsExpanded);
    }

    [Fact]
    public async Task SinPreferencia_ExpandidoConTodosLosGruposColapsados()
    {
        var menu = CreateMenu();

        await menu.SelectEntryCommand.ExecuteAsync(Group(menu, "catalogs").Children[0]);

        Assert.False(menu.IsCollapsed);
        Assert.All(menu.Items.Where(i => i.IsGroup), g => Assert.False(g.IsExpanded));
        Assert.True(Group(menu, "catalogs").IsCurrentGroup);
        Assert.Null(Saved);
    }

    [Fact]
    public void ConSesion_LeeYGuardaConLaClaveDelUsuario()
    {
        var session = new FakeUserSession();
        var key = $"navigation.{session.User!.Id:N}";
        _preferences.Save(key, new NavigationPreferences(false, ["inventory"]));
        _preferences.Save(MenuViewModel.PreferencesKey, new NavigationPreferences(false, ["catalogs"]));

        var menu = new MenuViewModel(_host.Get<NavigationRegistry>(), _host.Get<Navigator>(), _preferences, session: session);

        Assert.True(Group(menu, "inventory").IsExpanded);
        Assert.False(Group(menu, "catalogs").IsExpanded);
        Assert.Equal(key, MenuViewModel.KeyFor(session.User.Id));

        menu.ToggleGroupCommand.Execute(Group(menu, "sales"));

        Assert.Equal(["inventory", "sales"], _preferences.Load<NavigationPreferences>(key)!.ExpandedGroups.Order());
        Assert.Equal(["catalogs"], Saved!.ExpandedGroups);
    }

    [Fact]
    public void SinSesion_UsaLaClaveGlobal()
    {
        Assert.Equal("navigation", MenuViewModel.KeyFor(null));

        var menu = CreateMenu();
        menu.ToggleGroupCommand.Execute(Group(menu, "sales"));

        Assert.Equal(["sales"], Saved!.ExpandedGroups);
    }

    [Fact]
    public void ContraerYExpandirElMenu_NoAlteraLosGrupos()
    {
        _preferences.Save(MenuViewModel.PreferencesKey, new NavigationPreferences(false, ["inventory"]));
        var menu = CreateMenu();

        menu.ToggleCommand.Execute(null);
        menu.ToggleCommand.Execute(null);

        Assert.True(Group(menu, "inventory").IsExpanded);
        Assert.False(Group(menu, "sales").IsExpanded);
        Assert.Equal(["inventory"], Saved!.ExpandedGroups);
    }

    [Fact]
    public void VentanaAngosta_ContraeSinGuardar_YAlAmpliarVuelveAlEstadoElegido()
    {
        var menu = CreateMenu();

        menu.SetWindowWidth(999);

        Assert.True(menu.IsAutoCollapsed);
        Assert.True(menu.IsCollapsed);
        Assert.Equal(0, _preferences.SaveCount);

        menu.SetWindowWidth(1000);

        Assert.False(menu.IsCollapsed);
    }

    [Fact]
    public async Task SeleccionarOpcion_NavegaYMarcaLaOpcionYSuGrupo()
    {
        var menu = CreateMenu();
        var products = Group(menu, "catalogs").Children[0];

        await menu.SelectEntryCommand.ExecuteAsync(products);

        Assert.Equal("catalogs.products", _host.Get<Navigator>().CurrentEntryId);
        Assert.True(products.IsCurrent);
        Assert.True(Group(menu, "catalogs").IsCurrentGroup);
        Assert.False(Group(menu, "help").IsCurrentGroup);
        Assert.False(menu.Items[0].IsCurrent);
    }

    [Fact]
    public async Task NavegacionRechazada_NoCambiaLaMarca()
    {
        using var host = HomeTestSupport.CreateHostWithModules(s =>
        {
            s.AddSingleton<IPreferencesStore>(new InMemoryPreferencesStore());
            s.AddPage<TestPageViewModel, TestPageView>("test.page", "Prueba", "Icon.Chart", 5);
        });
        var menu = new MenuViewModel(host.Get<NavigationRegistry>(), host.Get<Navigator>(), host.Get<IPreferencesStore>());
        var testItem = menu.Items.Single(i => i.Id == "test.page");
        await menu.SelectEntryCommand.ExecuteAsync(testItem);
        host.Get<TestPageViewModel>().AllowLeave = false;
        var products = menu.Items.Single(i => i.Id == "catalogs").Children[0];

        await menu.SelectEntryCommand.ExecuteAsync(products);

        Assert.Equal("test.page", host.Get<Navigator>().CurrentEntryId);
        Assert.True(testItem.IsCurrent);
        Assert.False(products.IsCurrent);
    }

    [Fact]
    public async Task CualquierOpcion_SeAlcanzaEnDosSeleccionesOMenos()
    {
        var menu = CreateMenu();
        menu.ToggleCommand.Execute(null);

        foreach (var item in menu.Items)
        {
            // Primer nivel: 1 selección. Grupo: 1 para abrirlo (o el menú flotante) y 1 para la opción.
            var targets = item.IsGroup ? item.Children : [item];
            foreach (var target in targets)
            {
                await menu.SelectEntryCommand.ExecuteAsync(target);
                Assert.Equal(target.Id, _host.Get<Navigator>().CurrentEntryId);
            }
        }
    }
}
