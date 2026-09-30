using Pos.Application.Abstractions;
using Pos.Desktop.About;
using Pos.Desktop.Products;
using Pos.Desktop.Resources;
using Pos.Desktop.Tests.TestSupport;
using Pos.Domain.Common;
using Pos.Domain.Products;
using Serilog.Events;

namespace Pos.Desktop.Tests.Common;

/// <summary>
/// SC-008: un error inesperado en cualquier operación queda registrado con su contexto, el
/// operador ve un mensaje comprensible y la pantalla sigue usable sin perder lo capturado.
/// </summary>
public sealed class UnexpectedErrorTests : IDisposable
{
    private static readonly InvalidOperationException Failure = new("falla inesperada simulada");

    private readonly DesktopTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private ProductEditorViewModel CreateEditor() => new(_host.UseCases, _host.Runner, _host.Dialogs);

    private ProductsViewModel CreatePage() => new(_host.UseCases, _host.Runner, _host.Dialogs, CreateEditor);

    private Product Seed() =>
        _host.Repository.Seed(Product.Create("Café", "CAF-001", null, Money.FromCents(100), "H87"));

    private void AssertLoggedAndNotified(string operation)
    {
        var error = Assert.Single(_host.Sink.Events, e => e.Level == LogEventLevel.Error);
        Assert.Same(Failure, error.Exception);
        Assert.Equal($"\"{operation}\"", error.Properties["Operation"].ToString());
        Assert.Equal(FixedCurrentUser.Id.ToString(), error.Properties["UserId"].ToString());
        Assert.Equal(Strings.Common_UnexpectedError, Assert.Single(_host.Dialogs.Messages).Message);
    }

    [Fact]
    public async Task Alta_ConservaLoCapturadoYPermiteReintentar()
    {
        var editor = CreateEditor();
        (editor.Name, editor.Sku, editor.PriceText) = ("Café", "CAF-001", "10");
        _host.Repository.FailWith = Failure;

        await editor.SaveCommand.ExecuteAsync(null);

        AssertLoggedAndNotified("GuardarProducto");
        Assert.Equal(("Café", "CAF-001", "10"), (editor.Name, editor.Sku, editor.PriceText));
        Assert.Contains("Sku", _host.Sink.Events.Single(e => e.Level == LogEventLevel.Error).Properties.Keys);

        _host.Repository.FailWith = null;
        await editor.SaveCommand.ExecuteAsync(null);
        Assert.Single(_host.Repository.All);
    }

    [Fact]
    public async Task Busqueda_LaPantallaSigueUsable()
    {
        Seed();
        var page = CreatePage();
        page.SearchText = "café";
        _host.Repository.FailWith = Failure;

        await page.SearchNowCommand.ExecuteAsync(null);

        AssertLoggedAndNotified("BuscarProductos");
        Assert.Equal("café", page.SearchText);
        _host.Repository.FailWith = null;
        await page.SearchNowCommand.ExecuteAsync(null);
        Assert.Single(page.Items);
    }

    [Fact]
    public async Task Edicion_ConservaLoCapturado()
    {
        var product = Seed();
        var editor = CreateEditor();
        await editor.LoadAsync(product.Id);
        editor.Name = "Café de Olla";
        _host.Repository.FailWith = Failure;

        await editor.SaveCommand.ExecuteAsync(null);

        AssertLoggedAndNotified("GuardarProducto");
        Assert.Equal("Café de Olla", editor.Name);
        Assert.Equal(product.Id.ToString(), _host.Sink.Events.Single(e => e.Level == LogEventLevel.Error).Properties["ProductId"].ToString());
    }

    [Fact]
    public async Task Borrado_NoBorraYLaListaSigueDisponible()
    {
        Seed();
        var page = CreatePage();
        await page.OnActivatedAsync();
        page.SelectedItem = page.Items.Single();
        _host.Dialogs.ConfirmResult = true;
        _host.Repository.FailWith = Failure;

        await page.DeleteCommand.ExecuteAsync(null);

        Assert.Contains(_host.Sink.Events, e => e.Level == LogEventLevel.Error && e.Properties["Operation"].ToString() == "\"BorrarProducto\"");
        Assert.Contains(_host.Dialogs.Messages, m => m.Message == Strings.Common_UnexpectedError);
        _host.Repository.FailWith = null;
        Assert.False(_host.Repository.All.Single().IsDeleted);
        await page.SearchNowCommand.ExecuteAsync(null);
        Assert.Single(page.Items);
    }

    [Fact]
    public async Task Exportacion_ErrorNoPrevistoSeRegistraYSeInforma()
    {
        var about = new AboutViewModel(_host.UseCases, _host.Runner, _host.Dialogs, _host.Get<IClock>(), _host.Clipboard);
        _host.Dialogs.SaveFilePath = "/destino/diag.zip";
        _host.Exporter.FailWith = Failure;

        await about.ExportCommand.ExecuteAsync(null);

        AssertLoggedAndNotified("ExportarDiagnostico");
        Assert.False(about.IsExporting);
    }
}
