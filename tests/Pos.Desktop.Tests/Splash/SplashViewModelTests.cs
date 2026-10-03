using Pos.Application.Startup;
using Pos.Desktop.Resources;
using Pos.Desktop.Splash;
using Pos.Desktop.Tests.TestSupport;

namespace Pos.Desktop.Tests.Splash;

public class SplashViewModelTests
{
    private TimeSpan _elapsed;
    private readonly List<TimeSpan> _delays = [];

    private SplashViewModel Create() => new(
        new FakeAppInfo(),
        new FakeBranding(),
        () => _elapsed,
        delay =>
        {
            _delays.Add(delay);
            return Task.CompletedTask;
        });

    [Fact]
    public void AlCrearse_MuestraNombreVersionYTextoInicial()
    {
        var splash = Create();

        Assert.Equal(Strings.AppTitle, splash.AppName);
        Assert.Equal("0.1.0", splash.Version);
        Assert.Equal(Strings.Splash_Starting, splash.StepText);
    }

    [Theory]
    [InlineData(StartupStep.CheckingDatabase, "Splash_CheckingDatabase")]
    [InlineData(StartupStep.BackingUp, "Splash_BackingUp")]
    [InlineData(StartupStep.Migrating, "Splash_Migrating")]
    [InlineData(StartupStep.Restoring, "Splash_Restoring")]
    [InlineData(StartupStep.Finishing, "Splash_Finishing")]
    public void CadaPaso_MuestraSuTexto(StartupStep step, string resourceKey)
    {
        var splash = Create();

        ((IProgress<StartupStep>)splash).Report(step);

        Assert.Equal(Strings.ResourceManager.GetString(resourceKey, Strings.Culture), splash.StepText);
    }

    [Fact]
    public void EsperaMinima_EsDeTresSegundos()
    {
        Assert.Equal(TimeSpan.FromSeconds(3), SplashViewModel.MinimumVisible);
    }

    [Fact]
    public async Task EsperaMinima_CompletaLosTresSegundosEnArranqueRapido()
    {
        var splash = Create();
        _elapsed = TimeSpan.FromMilliseconds(300);

        await splash.WaitMinimumAsync();

        Assert.Equal([TimeSpan.FromMilliseconds(2700)], _delays);
    }

    [Theory]
    [InlineData(3000)]
    [InlineData(4500)]
    public async Task EsperaMinima_NoAgregaEsperaSiElArranqueYaTardoMas(int elapsedMilliseconds)
    {
        var splash = Create();
        _elapsed = TimeSpan.FromMilliseconds(elapsedMilliseconds);

        await splash.WaitMinimumAsync();

        Assert.Empty(_delays);
    }

    private sealed class FakeBranding : IBrandingAssets
    {
        public Avalonia.Media.IImage? Logo => null;
    }
}
