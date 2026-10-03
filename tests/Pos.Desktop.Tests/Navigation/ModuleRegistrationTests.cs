using Microsoft.Extensions.DependencyInjection;
using Pos.Desktop.About;
using Pos.Desktop.Administration;
using Pos.Desktop.CashShifts;
using Pos.Desktop.Categories;
using Pos.Desktop.Common;
using Pos.Desktop.Customers;
using Pos.Desktop.Discounts;
using Pos.Desktop.Home;
using Pos.Desktop.Inventory;
using Pos.Desktop.Licensing;
using Pos.Desktop.Navigation;
using Pos.Desktop.Products;
using Pos.Desktop.Purchases;
using Pos.Desktop.Reports;
using Pos.Desktop.Returns;
using Pos.Desktop.Sales;
using Pos.Desktop.Settings;
using Pos.Desktop.Tests.Home;
using Pos.Domain.Users;

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

        Assert.Equal(["home", "sales", "catalogs", "inventory", "administration", "settings", "help"], roots.Select(r => r.Id));
        Assert.Equal(["sales.pos", "sales.history"], roots[1].Children.Select(c => c.Id));
        Assert.Equal(["catalogs.products"], roots[2].Children.Select(c => c.Id));
        Assert.Equal(["inventory.stock", "inventory.movements"], roots[3].Children.Select(c => c.Id));
        Assert.Equal(["administration.users", "administration.audit"], roots[4].Children.Select(c => c.Id));
        Assert.Equal(["settings.business", "settings.printer", "settings.security", "settings.scanner-test"], roots[5].Children.Select(c => c.Id));
        Assert.Equal(["help.about"], roots[6].Children.Select(c => c.Id));
    }

    [Fact]
    public void ProbarEscaner_EstaEnConfiguracionSinPermiso()
    {
        var entries = AllModules().GetServices<NavigationEntry>().ToList();

        var scanner = Assert.Single(entries, e => e.Id == SettingsModule.ScannerTestPageId);
        Assert.Equal("settings.scanner-test", scanner.Id);
        Assert.Equal(SettingsModule.GroupId, scanner.GroupId);
        Assert.Equal(30, scanner.Order);
        Assert.Null(scanner.Permission);
        Assert.Equal("Icon.BarcodeScan", scanner.Icon);
        Assert.DoesNotContain(entries, e => e.GroupId == AboutModule.GroupId && e.Id != AboutModule.PageId);
    }

    [Fact]
    public void Cajero_VeConfiguracionSoloConProbarEscaner()
    {
        var registry = MenuFor(p => RolePermissions.Has(UserRole.Cashier, p));

        var settings = Assert.Single(registry.Roots, r => r.Id == SettingsModule.GroupId);
        Assert.Equal([SettingsModule.ScannerTestPageId], settings.Children.Select(c => c.Id));
    }

    [Fact]
    public void Administrador_MenuCompletoSinIconosRepetidos()
    {
        var registry = MenuFor(null);

        var icons = registry.Roots
            .SelectMany(r => r.Group is { } group ? [group.Icon, .. r.Children.Select(c => c.Icon)] : new[] { r.Entry!.Icon })
            .ToList();

        Assert.Equal(41, icons.Count);
        Assert.Empty(icons.GroupBy(i => i, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key));
    }

    /// <summary>Registros de menú de todos los módulos, en el orden de la aplicación.</summary>
    private static ServiceProvider AllModules()
    {
        var services = new ServiceCollection();
        services.AddHomeModule();
        services.AddProductsModule();
        services.AddCategoriesModule();
        services.AddInventoryModule();
        services.AddPurchasesModule();
        services.AddSalesModule();
        services.AddCashShiftsModule();
        services.AddCashModule();
        services.AddCustomersModule();
        services.AddReturnsModule();
        services.AddReportsModule();
        services.AddDiscountsModule();
        services.AddAdministrationModule();
        services.AddSettingsModule();
        services.AddLicenseModule();
        services.AddHelpModule();
        return services.BuildServiceProvider();
    }

    private static NavigationRegistry MenuFor(Func<Permission, bool>? isAllowed)
    {
        using var provider = AllModules();
        return new NavigationRegistry(provider.GetServices<NavigationGroup>(), provider.GetServices<NavigationEntry>(), isAllowed);
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
