using Pos.Desktop.Navigation;
using Pos.Desktop.Products;
using Pos.Desktop.Tests.Home;
using Pos.Desktop.Tests.TestSupport;
using Pos.Domain.Common;
using Pos.Domain.Products;

namespace Pos.Desktop.Tests.Navigation;

/// <summary>FR-020b: al regresar a una pantalla se conservan búsqueda, filtros y selección, y se refrescan los datos.</summary>
public sealed class PageStateRetentionTests : IDisposable
{
    private readonly DesktopTestHost _host = HomeTestSupport.CreateHostWithModules();

    public void Dispose() => _host.Dispose();

    private Navigator Navigator => _host.Get<Navigator>();

    private Product Seed(string name, string sku, bool active = true)
    {
        var product = Product.Create(name, sku, null, Money.FromCents(100));
        if (!active)
        {
            product.Update(name, sku, null, product.Price, isActive: false);
        }

        return _host.Repository.Seed(product);
    }

    private async Task<ProductsViewModel> OpenProductsWithStateAsync()
    {
        Seed("Leche entera", "LEC-1");
        Seed("Leche deslactosada", "LEC-2", active: false);
        Seed("Pan", "PAN-1");
        await Navigator.NavigateAsync("catalogs.products");
        var page = (ProductsViewModel)Navigator.CurrentPage!;
        page.SearchText = "leche";
        page.IncludeInactive = true;
        await page.SearchNowCommand.ExecuteAsync(null);
        page.SelectedItem = page.Items.Single(i => i.Sku == "LEC-2");
        return page;
    }

    [Fact]
    public async Task AlRegresar_ConservaBusquedaFiltroYSeleccion_YRefrescaLosDatos()
    {
        var page = await OpenProductsWithStateAsync();
        await Navigator.NavigateAsync("home");
        Seed("Leche de almendra", "LEC-3");

        await Navigator.NavigateAsync("catalogs.products");

        Assert.Same(page, Navigator.CurrentPage);
        Assert.Equal("leche", page.SearchText);
        Assert.True(page.IncludeInactive);
        Assert.Equal("LEC-2", page.SelectedItem?.Sku);
        Assert.Equal(["LEC-3", "LEC-2", "LEC-1"], page.Items.Select(i => i.Sku));
    }

    [Fact]
    public async Task SeleccionBorradaMientrasTanto_QuedaVaciaSinError()
    {
        var page = await OpenProductsWithStateAsync();
        await Navigator.NavigateAsync("home");
        _host.Repository.All.Single(p => p.Sku == "LEC-2").Delete(DateTime.UtcNow);

        await Navigator.NavigateAsync("catalogs.products");

        Assert.Null(page.SelectedItem);
        Assert.Empty(_host.Dialogs.Messages);
    }
}
