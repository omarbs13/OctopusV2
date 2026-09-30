using System.Globalization;
using Pos.Application.Abstractions;
using Pos.Desktop.About;
using Pos.Desktop.Resources;
using Pos.Desktop.Tests.TestSupport;

namespace Pos.Desktop.Tests.About;

public sealed class AboutViewModelTests : IDisposable
{
    private readonly DesktopTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private AboutViewModel Create() =>
        new(_host.UseCases, _host.Runner, _host.Dialogs, _host.Get<IClock>(), _host.Clipboard);

    [Fact]
    public async Task AlActivarse_MuestraVersionCarpetaDeDatosYSistemaOperativo()
    {
        var about = Create();

        await about.OnActivatedAsync();

        Assert.Equal(("0.1.0", "/datos/Pos", "Linux de prueba"), (about.Version, about.DataDirectory, about.OperatingSystem));
        Assert.Equal(Strings.Shell_NavAbout, about.Title);
    }

    [Fact]
    public async Task CopiarRuta_LlevaLaCarpetaDeDatosAlPortapapeles()
    {
        var about = Create();
        await about.OnActivatedAsync();

        await about.CopyDataDirectoryCommand.ExecuteAsync(null);

        Assert.Equal("/datos/Pos", _host.Clipboard.Text);
    }

    [Fact]
    public async Task Exportar_SugiereNombreConFechaLocalYMuestraLaRuta()
    {
        var about = Create();
        _host.Dialogs.SaveFilePath = "/destino/diag.zip";

        await about.ExportCommand.ExecuteAsync(null);

        var local = _host.Clock.UtcNow.ToLocalTime();
        Assert.Equal($"pos-diagnostico-{local:yyyyMMdd-HHmm}.zip", _host.Dialogs.LastSuggestedFileName);
        Assert.Equal("/destino/diag.zip", _host.Exporter.Destination);
        var expected = string.Format(CultureInfo.CurrentCulture, Strings.About_ExportDone, "/destino/diag.zip");
        Assert.Equal(expected, Assert.Single(_host.Dialogs.Messages).Message);
    }

    [Fact]
    public async Task Exportar_MuestraIndicadorMientrasSeEjecuta()
    {
        var about = Create();
        _host.Dialogs.SaveFilePath = "/destino/diag.zip";
        var gate = new TaskCompletionSource();
        _host.Exporter.Gate = gate;

        var export = about.ExportCommand.ExecuteAsync(null);
        Assert.True(about.IsExporting);

        gate.SetResult();
        await export;
        Assert.False(about.IsExporting);
    }

    [Fact]
    public async Task ExportacionFallida_MuestraMensajeComprensible()
    {
        var about = Create();
        _host.Dialogs.SaveFilePath = "/destino/diag.zip";
        _host.Exporter.FailWith = new IOException("disco lleno");

        await about.ExportCommand.ExecuteAsync(null);

        var message = Assert.Single(_host.Dialogs.Messages).Message;
        Assert.DoesNotContain("disco lleno", message, StringComparison.Ordinal);
        Assert.False(about.IsExporting);
    }

    [Fact]
    public async Task CancelarElSelector_NoExporta()
    {
        var about = Create();
        _host.Dialogs.SaveFilePath = null;

        await about.ExportCommand.ExecuteAsync(null);

        Assert.Null(_host.Exporter.Destination);
        Assert.Empty(_host.Dialogs.Messages);
    }
}
