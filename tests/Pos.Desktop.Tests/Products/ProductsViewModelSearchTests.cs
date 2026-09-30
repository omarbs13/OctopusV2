using Pos.Desktop.Products;
using Pos.Desktop.Tests.TestSupport;
using Pos.Domain.Common;
using Pos.Domain.Products;

namespace Pos.Desktop.Tests.Products;

public sealed class ProductsViewModelSearchTests : IDisposable
{
    private readonly DesktopTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private ProductsViewModel CreatePage() =>
        new(_host.UseCases, _host.Runner, _host.Dialogs, () => new ProductEditorViewModel(_host.UseCases, _host.Runner, _host.Dialogs));

    private Product Seed(string name, string sku, bool active = true)
    {
        var product = Product.Create(name, sku, null, Money.FromCents(12345), "H87");
        if (!active)
        {
            product.Update(name, sku, null, product.Price, "H87", isActive: false);
        }

        return _host.Repository.Seed(product);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AlActivarse_CargaLosProductosActivos()
    {
        Seed("Leche", "LEC-1");
        Seed("Pan", "PAN-1", active: false);
        var page = CreatePage();

        await page.OnActivatedAsync();

        Assert.Equal(["LEC-1"], page.Items.Select(i => i.Sku));
        Assert.False(page.IsEmpty);
    }

    [Fact]
    public async Task Escribir_BuscaDespuesDe250msDeLaUltimaTecla()
    {
        Seed("Leche", "LEC-1");
        Seed("Pan", "PAN-1");
        var page = CreatePage();
        await page.OnActivatedAsync();

        page.SearchText = "le";
        page.SearchText = "lec";
        Assert.Equal(2, page.Items.Count);

        await Task.Delay(ProductsViewModel.SearchDelay + TimeSpan.FromMilliseconds(300), Ct);

        Assert.Equal(["LEC-1"], page.Items.Select(i => i.Sku));
    }

    [Fact]
    public async Task Enter_BuscaDeInmediato()
    {
        Seed("Leche", "LEC-1");
        Seed("Pan", "PAN-1");
        var page = CreatePage();
        page.SearchText = "pan";

        await page.SearchNowCommand.ExecuteAsync(null);

        Assert.Equal(["PAN-1"], page.Items.Select(i => i.Sku));
    }

    [Fact]
    public async Task SinResultados_IndicaListaVacia()
    {
        Seed("Leche", "LEC-1");
        var page = CreatePage();
        page.SearchText = "inexistente";

        await page.SearchNowCommand.ExecuteAsync(null);

        Assert.Empty(page.Items);
        Assert.True(page.IsEmpty);
    }

    [Fact]
    public async Task MasDe100Resultados_MuestraLaPrimeraPaginaYElTotal()
    {
        for (var i = 0; i < 201; i++)
        {
            Seed($"Producto {i:000}", $"P-{i:000}");
        }

        var page = CreatePage();
        await page.OnActivatedAsync();

        Assert.Equal(100, page.Items.Count);
        Assert.Equal(201, page.TotalCount);
        Assert.Equal(1, page.CurrentPage);
        Assert.Equal(3, page.TotalPages);
    }

    [Fact]
    public async Task FiltroMostrarInactivos_VuelveABuscarIncluyendolos()
    {
        Seed("Leche", "LEC-1");
        Seed("Pan", "PAN-1", active: false);
        var page = CreatePage();
        await page.OnActivatedAsync();

        page.IncludeInactive = true;
        await WaitUntilAsync(() => page.Items.Count == 2);

        Assert.Equal(["LEC-1", "PAN-1"], page.Items.Select(i => i.Sku));
    }

    [Fact]
    public async Task NuevoProducto_AlGuardar_CierraElEditorYSeleccionaElProductoEnLaLista()
    {
        Seed("Leche", "LEC-1");
        var page = CreatePage();
        await page.OnActivatedAsync();

        page.NewProductCommand.Execute(null);
        var editor = Assert.IsType<ProductEditorViewModel>(page.Editor);
        editor.Name = "Café";
        editor.Sku = "caf-1";
        editor.PriceText = "10";
        await editor.SaveCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => page.SelectedItem is not null);

        Assert.Null(page.Editor);
        Assert.Equal("CAF-1", page.SelectedItem!.Sku);
        Assert.Contains(page.Items, i => i.Sku == "CAF-1");
    }

    [Fact]
    public async Task NuevoProductoFueraDelFiltroActual_LimpiaLaBusquedaParaMostrarlo()
    {
        Seed("Leche", "LEC-1");
        var page = CreatePage();
        page.SearchText = "leche";
        await page.SearchNowCommand.ExecuteAsync(null);

        page.NewProductCommand.Execute(null);
        page.Editor!.Name = "Café";
        page.Editor.Sku = "caf-1";
        page.Editor.PriceText = "10";
        await page.Editor.SaveCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => page.SelectedItem is not null);

        Assert.Equal(string.Empty, page.SearchText);
        Assert.Equal("CAF-1", page.SelectedItem!.Sku);
    }

    [Fact]
    public async Task CancelarElEditor_LoCierraSinCambios()
    {
        var page = CreatePage();
        page.NewProductCommand.Execute(null);

        page.Editor!.CancelCommand.Execute(null);
        await Task.CompletedTask;

        Assert.Null(page.Editor);
        Assert.Empty(_host.Repository.All);
    }

    [Fact]
    public async Task ErrorInesperadoAlBuscar_SeRegistraYLaPantallaSigueUsable()
    {
        Seed("Leche", "LEC-1");
        var page = CreatePage();
        _host.Repository.FailWith = new InvalidOperationException("falla simulada");

        await page.SearchNowCommand.ExecuteAsync(null);

        Assert.Single(_host.Dialogs.Messages);
        _host.Repository.FailWith = null;
        await page.SearchNowCommand.ExecuteAsync(null);
        Assert.Single(page.Items);
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
