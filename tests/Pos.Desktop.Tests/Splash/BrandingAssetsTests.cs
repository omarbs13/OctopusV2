using Pos.Desktop.Splash;
using Pos.Desktop.Tests.TestSupport;
using Serilog.Events;

namespace Pos.Desktop.Tests.Splash;

public sealed class BrandingAssetsTests : IDisposable
{
    // PNG válido de 1 x 1 píxel.
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pos-branding-" + Guid.NewGuid().ToString("N"));
    private readonly CollectingSink _sink = new();

    public BrandingAssetsTests() => Directory.CreateDirectory(_dir);

    private string LogoFile => Path.Combine(_dir, "logo.png");

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void SinArchivo_UsaElPredeterminado()
    {
        Assert.Null(BrandingAssets.ResolveCustomLogo(LogoFile, _sink.CreateLogger()));
        Assert.Empty(_sink.Events);
    }

    [Fact]
    public void ConPngValido_UsaEseArchivo()
    {
        File.WriteAllBytes(LogoFile, TinyPng);

        Assert.Equal(LogoFile, BrandingAssets.ResolveCustomLogo(LogoFile, _sink.CreateLogger()));
    }

    [Fact]
    public void ConArchivoQueNoEsImagen_UsaElPredeterminadoYAdvierte()
    {
        File.WriteAllText(LogoFile, "esto no es una imagen");

        Assert.Null(BrandingAssets.ResolveCustomLogo(LogoFile, _sink.CreateLogger()));
        Assert.Contains(_sink.Events, e => e.Level == LogEventLevel.Warning);
    }
}
