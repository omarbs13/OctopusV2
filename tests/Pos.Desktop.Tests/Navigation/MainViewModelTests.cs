using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Abstractions;
using Pos.Desktop.Navigation;
using Pos.Desktop.Shell;
using Pos.Desktop.Tests.TestSupport;

namespace Pos.Desktop.Tests.Navigation;

public class MainViewModelTests
{
    private static ServiceProvider Create(bool withHome)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new CollectingSink().CreateLogger());
        services.AddSingleton<IAppInfo, FakeAppInfo>();
        services.AddSingleton<IPreferencesStore>(new InMemoryPreferencesStore());
        services.AddNavigationCore();
        services.AddNavigationGroup("group", "Grupo", "Icon.Catalog", 10);
        services.AddPage<OtherTestPageViewModel, TestPageView>("group.other", "Otra", "Icon.Product", 0, "group");
        if (withHome)
        {
            services.AddPage<TestPageViewModel, TestPageView>("home", "Inicio", "Icon.Home", 0);
        }

        services.AddSingleton<MainViewModel>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Start_AbreInicio()
    {
        using var provider = Create(withHome: true);
        var main = provider.GetRequiredService<MainViewModel>();

        await main.StartAsync();

        Assert.Equal("home", main.Navigator.CurrentEntryId);
        Assert.Same(main.Navigator.CurrentPage, main.CurrentPage);
        Assert.Equal("POS 0.1.0", main.WindowTitle);
    }

    [Fact]
    public async Task Start_SinInicio_AbreLaPrimeraOpcionRegistrada()
    {
        using var provider = Create(withHome: false);
        var main = provider.GetRequiredService<MainViewModel>();

        await main.StartAsync();

        Assert.Equal("group.other", main.Navigator.CurrentEntryId);
    }
}
