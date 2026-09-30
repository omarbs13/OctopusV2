using System.Diagnostics;
using Pos.Application.Products.CreateProduct;
using Pos.Application.Products.SearchProducts;
using Pos.Domain.Common;
using Pos.Domain.Products;
using Pos.Infrastructure.Products;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Products;

/// <summary>SC-011: con 10,000 productos, búsqueda y alta visibles en menos de 1 segundo.</summary>
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
            Product.Create($"Artículo de prueba número {i:00000} café", $"SKU-{i:00000}", $"750{i:0000000000}", Money.FromCents(i))));
        await context.SaveChangesAsync(Ct);

        // Calentamiento: la primera consulta compila el modelo de EF Core.
        await SearchAsync("calentamiento");
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<SearchProductsResult> SearchAsync(string text)
    {
        await using var context = _db.CreateDbContext();
        return (await new SearchProductsHandler(new ProductRepository(context))
            .HandleAsync(new SearchProductsQuery(text, IncludeInactive: false), Ct)).Value;
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
            var created = await new CreateProductHandler(new ProductRepository(context), new CreateProductValidator())
                .HandleAsync(new CreateProductCommand("Nuevo producto", "NUEVO-1", null, "10.00"), Ct);
            Assert.True(created.IsSuccess);
        }

        var result = await SearchAsync("nuevo-1");

        watch.Stop();
        Assert.Equal(["NUEVO-1"], result.Items.Select(i => i.Sku));
        Assert.True(watch.Elapsed < Budget, $"El alta y la búsqueda tardaron {watch.Elapsed.TotalMilliseconds:0} ms");
    }
}
