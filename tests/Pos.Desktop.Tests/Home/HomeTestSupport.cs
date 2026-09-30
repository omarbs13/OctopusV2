using Microsoft.Extensions.DependencyInjection;
using Pos.Desktop.About;
using Pos.Desktop.Administration;
using Pos.Desktop.Common;
using Pos.Desktop.Home;
using Pos.Desktop.Inventory;
using Pos.Desktop.Navigation;
using Pos.Desktop.Products;
using Pos.Desktop.Sales;
using Pos.Desktop.Settings;
using Pos.Desktop.Shell;
using Pos.Desktop.Tests.TestSupport;

namespace Pos.Desktop.Tests.Home;

/// <summary>Host con los módulos reales de la aplicación, más registros adicionales opcionales.</summary>
public static class HomeTestSupport
{
    public static DesktopTestHost CreateHostWithModules(Action<IServiceCollection>? extra = null) =>
        new(services =>
        {
            services.AddNavigationCore();
            services.AddHomeModule();
            services.AddProductsModule();
            services.AddInventoryModule();
            services.AddSalesModule();
            services.AddAdministrationModule();
            services.AddScoped<ModalHost>();
            services.AddSettingsModule();
            services.AddHelpModule();
            extra?.Invoke(services);
        });
}

/// <summary>Tarjeta que siempre falla al cargar.</summary>
public sealed class FailingCard(OperationRunner runner) : DashboardCard(runner)
{
    public override string Title => "Tarjeta que falla";

    public override string Icon => "Icon.Warning";

    public override int Order => 5;

    public override DashboardCardKind Kind => DashboardCardKind.Metric;

    protected override Task LoadCoreAsync() => throw new InvalidOperationException("falla simulada");
}
