using Pos.Application.Products;
using Pos.Application.Products.SearchProducts;
using Pos.Domain.Common;
using Pos.Domain.Products;
using Pos.Infrastructure.Products;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Products;

/// <summary>Paginación y filtro de inactivos sobre SQLite real (FR-001 a FR-012 de 003; SC-001).</summary>
public sealed class ProductPagingTests : IAsyncLifetime
{
    private TestDb _db = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _db = await TestDb.CreateAsync();

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task SeedAsync(IEnumerable<Product> products)
    {
        await using var context = _db.CreateDbContext();
        context.Products.AddRange(products);
        await context.SaveChangesAsync(Ct);
    }

    private async Task<ProductPage> SearchAsync(string? text = null, bool includeInactive = false, int page = 1, Guid? locate = null)
    {
        await using var context = _db.CreateDbContext();
        var result = await new SearchProductsHandler(new AllowAllAccessControl(), new ProductRepository(context))
            .HandleAsync(new SearchProductsQuery(text, includeInactive, page, locate), Ct);
        return result.Value;
    }

    private static Product P(string name, string sku, bool active = true, bool deleted = false)
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

        return product;
    }

    private static IEnumerable<Product> Many(int count, string prefix = "Producto") =>
        Enumerable.Range(1, count).Select(i => P($"{prefix} {i:000}", $"{prefix[..3].ToUpperInvariant()}-{i:000}"));

    [Fact]
    public async Task PaginasDe100_ConTotalesYOrdenPorNombre()
    {
        await SeedAsync(Many(250));

        var first = await SearchAsync();
        var third = await SearchAsync(page: 3);

        Assert.Equal((250L, 3, 1), (first.TotalCount, first.TotalPages, first.Page));
        Assert.Equal(100, first.Items.Count);
        Assert.Equal("Producto 001", first.Items[0].Name);
        Assert.Equal(50, third.Items.Count);
        Assert.Equal("Producto 201", third.Items[0].Name);
        Assert.Equal("Producto 250", third.Items[^1].Name);
    }

    [Fact]
    public async Task NombresIguales_OrdenEstablePorSku()
    {
        await SeedAsync(Enumerable.Range(1, 150).Select(i => P("Mismo nombre", $"S-{150 - i:000}")));

        var first = await SearchAsync();
        var second = await SearchAsync(page: 2);
        var skus = first.Items.Concat(second.Items).Select(i => i.Sku).ToList();

        Assert.Equal(skus.Order(StringComparer.Ordinal), skus);
        Assert.Equal(150, skus.Distinct().Count());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task PaginaMenorQueUno_DevuelveLaPrimera(int requested)
    {
        await SeedAsync(Many(150));

        Assert.Equal(1, (await SearchAsync(page: requested)).Page);
    }

    [Fact]
    public async Task PaginaFueraDeRango_DevuelveLaUltimaValida()
    {
        await SeedAsync(Many(150));

        var page = await SearchAsync(page: 9);

        Assert.Equal(2, page.Page);
        Assert.Equal(50, page.Items.Count);
    }

    [Fact]
    public async Task SinResultados_PaginaUnoDeUnoVacia()
    {
        await SeedAsync(Many(5));

        var page = await SearchAsync("inexistente", page: 4);

        Assert.Equal((0L, 1, 1), (page.TotalCount, page.TotalPages, page.Page));
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task FiltroDeInactivos_SeCombinaConTextoYPaginacion_SinBorrados()
    {
        await SeedAsync(Many(210, "Leche").Concat(
        [
            P("Leche ZZ inactiva", "LZZ-1", active: false),
            P("Leche ZZ borrada", "LZZ-2", deleted: true),
            P("Leche ZZ inactiva borrada", "LZZ-3", active: false, deleted: true),
            P("Pan inactivo", "PAN-1", active: false),
        ]));

        var activeOnly = await SearchAsync("leche", page: 3);
        var withInactive = await SearchAsync("leche", includeInactive: true, page: 3);

        Assert.Equal(210, activeOnly.TotalCount);
        Assert.DoesNotContain(activeOnly.Items, i => !i.IsActive);
        Assert.Equal(211, withInactive.TotalCount);
        Assert.Contains(withInactive.Items, i => i.Sku == "LZZ-1");
        Assert.DoesNotContain(withInactive.Items, i => i.Sku is "LZZ-2" or "LZZ-3" or "PAN-1");
    }

    [Fact]
    public async Task LocalizarProducto_DevuelveLaPaginaQueLoContiene()
    {
        var products = Many(250).ToList();
        await SeedAsync(products);
        var target = products.Single(p => p.Sku == "PRO-150");

        var page = await SearchAsync(page: 1, locate: target.Id);

        Assert.Equal(2, page.Page);
        Assert.Contains(page.Items, i => i.Id == target.Id);
    }

    [Fact]
    public async Task LocalizarProductoNoVisible_UsaLaPaginaSolicitada()
    {
        var hidden = P("Oculto", "OCU-1", active: false);
        await SeedAsync(Many(250).Append(hidden));

        var page = await SearchAsync(page: 3, locate: hidden.Id);

        Assert.Equal(3, page.Page);
        Assert.DoesNotContain(page.Items, i => i.Id == hidden.Id);
    }
}
