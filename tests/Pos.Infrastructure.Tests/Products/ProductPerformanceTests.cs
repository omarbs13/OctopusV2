using System.Diagnostics;
using Pos.Application.Products;
using Pos.Application.Products.CreateProduct;
using Pos.Application.Products.SearchProducts;
using Pos.Domain.Common;
using Pos.Domain.Products;
using Pos.Infrastructure.Products;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Products;

/// <summary>
/// SC-011 de 001 y SC-002 de 003: con 10,000 productos (500 inactivos), búsqueda, alta, cambio de
/// página y cambio de filtro en menos de 1 segundo.
/// </summary>
public sealed class ProductPerformanceTests : IAsyncLifetime
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(1);

    private TestDb _db = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDb.CreateAsync();
        await using var context = _db.CreateDbContext();
        context.Products.AddRange(Enumerable.Range(1, 10_000).Select(i =>
        {
            var product = Product.Create($"Artículo de prueba número {i:00000} café", $"SKU-{i:00000}", $"750{i:0000000000}", Money.FromCents(i), "H87");
            if (i % 20 == 0)
            {
                product.Update(product.Name, product.Sku, product.Barcode, product.Price, "H87", isActive: false);
            }

            return product;
        }));
        await context.SaveChangesAsync(Ct);

        // Calentamiento: la primera consulta compila el modelo de EF Core.
        await SearchAsync("calentamiento");
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<ProductPage> SearchAsync(string? text, bool includeInactive = false, int page = 1)
    {
        await using var context = _db.CreateDbContext();
        return (await new SearchProductsHandler(new AllowAllAccessControl(), new ProductRepository(context))
            .HandleAsync(new SearchProductsQuery(text, includeInactive, page), Ct)).Value;
    }

    private static async Task<(ProductPage Page, TimeSpan Elapsed)> MeasureAsync(Func<Task<ProductPage>> action)
    {
        var watch = Stopwatch.StartNew();
        var page = await action();
        watch.Stop();
        return (page, watch.Elapsed);
    }

    [Fact]
    public async Task PrimeraPagina_RespondeEnMenosDeUnSegundo()
    {
        var (page, elapsed) = await MeasureAsync(() => SearchAsync(null));

        Assert.Equal(100, page.Items.Count);
        Assert.Equal(9_500, page.TotalCount);
        Assert.True(elapsed < Budget, $"La primera página tardó {elapsed.TotalMilliseconds:0} ms");
    }

    [Fact]
    public async Task UltimaPagina_RespondeEnMenosDeUnSegundo()
    {
        var (page, elapsed) = await MeasureAsync(() => SearchAsync(null, page: 95));

        Assert.Equal(95, page.Page);
        Assert.Equal(100, page.Items.Count);
        Assert.True(elapsed < Budget, $"La última página tardó {elapsed.TotalMilliseconds:0} ms");
    }

    [Fact]
    public async Task CambioDeFiltroAInactivos_RespondeEnMenosDeUnSegundo()
    {
        var (page, elapsed) = await MeasureAsync(() => SearchAsync(null, includeInactive: true));

        Assert.Equal(10_000, page.TotalCount);
        Assert.Equal(100, page.TotalPages);
        Assert.True(elapsed < Budget, $"El cambio de filtro tardó {elapsed.TotalMilliseconds:0} ms");
    }

    [Fact]
    public async Task BusquedaParcialPorNombre_RespondeEnMenosDeUnSegundo()
    {
        var watch = Stopwatch.StartNew();
        var result = await SearchAsync("numero 0999");

        watch.Stop();
        Assert.Equal(10, result.Items.Count);
        Assert.True(watch.Elapsed < Budget, $"La búsqueda tardó {watch.Elapsed.TotalMilliseconds:0} ms");
    }

    [Fact]
    public async Task AltaYBusquedaDelNuevoProducto_EnMenosDeUnSegundo()
    {
        var watch = Stopwatch.StartNew();
        await using (var context = _db.CreateDbContext())
        {
            var created = await new CreateProductHandler(new AllowAllAccessControl(), new ProductRepository(context), new CreateProductValidator())
                .HandleAsync(new CreateProductCommand("Nuevo producto", "NUEVO-1", null, "10.00", "H87"), Ct);
            Assert.True(created.IsSuccess);
        }

        var result = await SearchAsync("nuevo-1");

        watch.Stop();
        Assert.Equal(["NUEVO-1"], result.Items.Select(i => i.Sku));
        Assert.True(watch.Elapsed < Budget, $"El alta y la búsqueda tardaron {watch.Elapsed.TotalMilliseconds:0} ms");
    }
}
