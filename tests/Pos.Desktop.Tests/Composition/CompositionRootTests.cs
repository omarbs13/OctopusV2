using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;
using Pos.Desktop.Composition;
using Pos.Desktop.Navigation;
using Pos.Desktop.Shell;
using Pos.Infrastructure.Platform;
using Pos.Desktop.Tests.TestSupport;

namespace Pos.Desktop.Tests.Composition;

/// <summary>
/// El grafo real de la aplicación se valida completo (ciclos de vida incluidos): ningún singleton captura
/// algo de la sesión, y cada sesión resuelve sus pantallas y su menú desde su propio ámbito (007, research §12).
/// </summary>
public sealed class CompositionRootTests : IDisposable
{
    private readonly TempDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        HostBuilder.ConfigureServices(
            services,
            new AppPaths(_directory.Path),
            new CollectingSink().CreateLogger(),
            new ClassicDesktopStyleApplicationLifetime());
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    [Fact]
    public void ElGrafoCompleto_EsValidoYNingunSingletonCapturaServiciosDeLaSesion()
    {
        using var provider = BuildProvider();

        Assert.NotNull(provider.GetRequiredService<RootViewModel>());
    }

    [Fact]
    public void CadaSesion_ResuelveSusPropiasPantallasYMenu()
    {
        using var provider = BuildProvider();

        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var mainA = first.ServiceProvider.GetRequiredService<MainViewModel>();
        var mainB = second.ServiceProvider.GetRequiredService<MainViewModel>();

        Assert.NotSame(mainA, mainB);
        Assert.NotSame(mainA.Menu, mainB.Menu);
        Assert.NotSame(first.ServiceProvider.GetRequiredService<Navigator>(), second.ServiceProvider.GetRequiredService<Navigator>());
    }
}

internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pos-desktop-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
