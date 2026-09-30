using Pos.Desktop.Products;
using Pos.Desktop.Tests.TestSupport;
using Pos.Domain.Common;
using Pos.Domain.Products;

namespace Pos.Desktop.Tests.Products;

/// <summary>
/// Reproduce el defecto de "Mostrar inactivos" (003, FR-005): el listado se cortaba en 200 filas
/// ordenadas por nombre, así que en un catálogo grande los inactivos que quedaban después del
/// corte nunca aparecían al activar la casilla.
/// </summary>
public sealed class ProductsViewModelInactiveFilterTests : IDisposable
{
    private readonly DesktopTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private ProductsViewModel CreatePage() =>
        new(_host.UseCases, _host.Runner, _host.Dialogs, () => new ProductEditorViewModel(_host.UseCases, _host.Runner, _host.Dialogs));

    private void Seed(string name, string sku, bool active = true, bool deleted = false)
    {
        var product = Product.Create(name, sku, null, Money.FromCents(100), "H87");
        if (!active)
        {
            product.Update(name, sku, null, product.Price, "H87", isActive: false);
        }

        if (deleted)
        {
            product.Delete(DateTime.UtcNow);
        }

        _host.Repository.Seed(product);
    }

    private void SeedLargeCatalog()
    {
        for (var i = 1; i <= 210; i++)
        {
            Seed($"Producto {i:000}", $"P-{i:000}");
        }

        Seed("ZZ Inactivo", "ZZ-1", active: false);
        Seed("ZZ Borrado", "ZZ-2", deleted: true);
    }

    [Fact]
    public async Task CatalogoGrande_ConLaCasillaActivada_IncluyeAlInactivoDespuesDelCorte()
    {
        SeedLargeCatalog();
        var page = CreatePage();
        await page.OnActivatedAsync();

        page.IncludeInactive = true;
        await WaitUntilAsync(() => page.TotalCount == 211);
        page.CurrentPage = page.TotalPages;
        await page.SearchNowCommand.ExecuteAsync(null);

        Assert.Contains(page.Items, i => i.Sku == "ZZ-1" && !i.IsActive);
        Assert.DoesNotContain(page.Items, i => i.Sku == "ZZ-2");
    }

    [Fact]
    public async Task CatalogoGrande_ConLaCasillaDesactivada_SoloActivos()
    {
        SeedLargeCatalog();
        var page = CreatePage();

        await page.OnActivatedAsync();
        page.CurrentPage = page.TotalPages;
        await page.SearchNowCommand.ExecuteAsync(null);

        Assert.Equal(210, page.TotalCount);
        Assert.All(page.Items, i => Assert.True(i.IsActive));
        Assert.DoesNotContain(page.Items, i => i.Sku is "ZZ-1" or "ZZ-2");
    }

    [Fact]
    public async Task CambiarLaCasilla_VuelveALaPrimeraPagina()
    {
        SeedLargeCatalog();
        var page = CreatePage();
        await page.OnActivatedAsync();
        page.CurrentPage = 3;
        await page.SearchNowCommand.ExecuteAsync(null);

        page.IncludeInactive = true;
        await WaitUntilAsync(() => page.TotalCount == 211);

        Assert.Equal(1, page.CurrentPage);
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
