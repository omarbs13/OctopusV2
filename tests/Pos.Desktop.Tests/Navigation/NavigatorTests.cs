using Microsoft.Extensions.DependencyInjection;
using Pos.Desktop.Navigation;
using Pos.Desktop.Tests.TestSupport;
using Serilog.Events;

namespace Pos.Desktop.Tests.Navigation;

public sealed class NavigatorTests : IDisposable
{
    private readonly CollectingSink _sink = new();
    private readonly ServiceProvider _provider;

    public NavigatorTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_sink.CreateLogger());
        services.AddNavigationCore();
        services.AddNavigationGroup("group", "Grupo", "Icon.Catalog", 10);
        services.AddPage<TestPageViewModel, TestPageView>("home", "Inicio", "Icon.Home", 0);
        services.AddPage<OtherTestPageViewModel, TestPageView>("group.other", "Otra", "Icon.Product", 0, "group");
        _provider = services.BuildServiceProvider();
    }

    private Navigator Navigator => _provider.GetRequiredService<Navigator>();

    public void Dispose() => _provider.Dispose();

    [Fact]
    public async Task Navegar_AsignaLaPantallaActualYLaActiva()
    {
        var changes = 0;
        Navigator.CurrentChanged += (_, _) => changes++;

        Assert.True(await Navigator.NavigateAsync("home"));

        Assert.Equal("home", Navigator.CurrentEntryId);
        var page = Assert.IsType<TestPageViewModel>(Navigator.CurrentPage);
        Assert.Equal(1, page.Activations);
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task NavegarALaOpcionActual_NoVuelveAActivar()
    {
        await Navigator.NavigateAsync("home");

        Assert.True(await Navigator.NavigateAsync("home"));

        Assert.Equal(1, ((TestPageViewModel)Navigator.CurrentPage!).Activations);
        Assert.Equal(0, ((TestPageViewModel)Navigator.CurrentPage!).LeaveQuestions);
    }

    [Fact]
    public async Task PantallaQueNoPermiteSalir_NoSeNavega()
    {
        await Navigator.NavigateAsync("home");
        var home = (TestPageViewModel)Navigator.CurrentPage!;
        home.AllowLeave = false;

        Assert.False(await Navigator.NavigateAsync("group.other"));

        Assert.Equal("home", Navigator.CurrentEntryId);
        Assert.Same(home, Navigator.CurrentPage);
        Assert.Equal(1, home.LeaveQuestions);
    }

    [Fact]
    public async Task OpcionInexistente_NoNavegaYSeRegistra()
    {
        await Navigator.NavigateAsync("home");

        Assert.False(await Navigator.NavigateAsync("noexiste"));

        Assert.Equal("home", Navigator.CurrentEntryId);
        Assert.Contains(_sink.Events, e => e.Level >= LogEventLevel.Warning && e.RenderMessage(System.Globalization.CultureInfo.InvariantCulture).Contains("noexiste", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AlVolverAUnaPantalla_EsLaMismaInstancia()
    {
        await Navigator.NavigateAsync("home");
        var first = Navigator.CurrentPage;
        await Navigator.NavigateAsync("group.other");

        await Navigator.NavigateAsync("home");

        Assert.Same(first, Navigator.CurrentPage);
        Assert.Equal(2, ((TestPageViewModel)first!).Activations);
    }

    [Fact]
    public async Task CanLeaveCurrent_DelegaEnLaPantallaActual()
    {
        Assert.True(await Navigator.CanLeaveCurrentAsync());
        await Navigator.NavigateAsync("home");
        ((TestPageViewModel)Navigator.CurrentPage!).AllowLeave = false;

        Assert.False(await Navigator.CanLeaveCurrentAsync());
    }
}
