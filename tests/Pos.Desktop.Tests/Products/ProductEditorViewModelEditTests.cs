using Pos.Desktop.Products;
using Pos.Desktop.Resources;
using Pos.Desktop.Tests.TestSupport;
using Pos.Domain.Common;
using Pos.Domain.Products;

namespace Pos.Desktop.Tests.Products;

public sealed class ProductEditorViewModelEditTests : IDisposable
{
    private readonly DesktopTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private ProductEditorViewModel CreateEditor() => new(_host.UseCases, _host.Runner, _host.Dialogs);

    private ProductsViewModel CreatePage() => new(_host.UseCases, _host.Runner, _host.Dialogs, CreateEditor);

    private Product Seed() =>
        _host.Repository.Seed(Product.Create("Café Molido", "CAF-001", "7501234567890", Money.FromCents(123450), "H87"));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Cargar_LlenaLosCamposEnModoEdicion()
    {
        var product = Seed();
        var editor = CreateEditor();

        Assert.True(await editor.LoadAsync(product.Id));

        Assert.True(editor.IsEditMode);
        Assert.Equal(Strings.Editor_EditTitle, editor.Title);
        Assert.Equal(("Café Molido", "CAF-001", "7501234567890", "1234.50", true),
            (editor.Name, editor.Sku, editor.Barcode, editor.PriceText, editor.IsActive));
    }

    [Fact]
    public async Task GuardarCambios_ActualizaElProducto()
    {
        var product = Seed();
        var editor = CreateEditor();
        await editor.LoadAsync(product.Id);
        editor.Name = "Café de Olla";

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Café de Olla", _host.Repository.All.Single().Name);
    }

    [Fact]
    public async Task Conflicto_AlAceptarRecarga_ReemplazaConLosDatosActuales()
    {
        var product = Seed();
        var editor = CreateEditor();
        await editor.LoadAsync(product.Id);
        _host.Repository.BumpVersion(product.Id);
        editor.Name = "Mi cambio";
        _host.Dialogs.ConfirmResult = true;

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Equal(Strings.Editor_Conflict, Assert.Single(_host.Dialogs.Confirmations));
        Assert.Equal("Café Molido", editor.Name);

        editor.Name = "Mi cambio";
        await editor.SaveCommand.ExecuteAsync(null);
        Assert.Equal("Mi cambio", _host.Repository.All.Single().Name);
    }

    [Fact]
    public async Task Conflicto_AlRechazarRecarga_ConservaLoCapturado()
    {
        var product = Seed();
        var editor = CreateEditor();
        await editor.LoadAsync(product.Id);
        _host.Repository.BumpVersion(product.Id);
        editor.Name = "Mi cambio";
        _host.Dialogs.ConfirmResult = false;

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Mi cambio", editor.Name);
        Assert.Equal("Café Molido", _host.Repository.All.Single().Name);
    }

    [Fact]
    public async Task ProductoYaNoExiste_MuestraMensajeYCierraElEditor()
    {
        var product = Seed();
        var editor = CreateEditor();
        await editor.LoadAsync(product.Id);
        var stored = _host.Repository.All.Single();
        stored.Delete(DateTime.UtcNow);
        var closed = false;
        editor.Closed += (_, _) => closed = true;

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Equal(Strings.Editor_NotFound, Assert.Single(_host.Dialogs.Messages).Message);
        Assert.True(closed);
    }

    [Fact]
    public async Task EditarDesdeLaLista_DesmarcarActivo_ElProductoDesapareceDelListadoPorDefecto()
    {
        Seed();
        var page = CreatePage();
        await page.OnActivatedAsync();
        page.SelectedItem = page.Items.Single();

        await page.EditCommand.ExecuteAsync(null);
        var editor = Assert.IsType<ProductEditorViewModel>(page.Editor);
        editor.IsActive = false;
        await editor.SaveCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => page.Editor is null && page.Items.Count == 0);

        Assert.Empty(page.Items);
        page.IncludeInactive = true;
        await WaitUntilAsync(() => page.Items.Count == 1);
        Assert.False(page.Items.Single().IsActive);
    }

    [Fact]
    public async Task Editar_SinSeleccion_NoEstaDisponible()
    {
        var page = CreatePage();
        await page.OnActivatedAsync();

        Assert.False(page.EditCommand.CanExecute(null));
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 300 && !condition(); i++)
        {
            await Task.Delay(10, Ct);
        }

        Assert.True(condition());
    }
}
