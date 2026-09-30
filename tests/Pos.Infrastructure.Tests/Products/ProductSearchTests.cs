using Pos.Application.Products;
using Pos.Application.Products.SearchProducts;
using Pos.Domain.Common;
using Pos.Domain.Products;
using Pos.Infrastructure.Products;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Products;

/// <summary>Búsqueda de punta a punta: caso de uso real sobre SQLite real.</summary>
public sealed class ProductSearchTests : IAsyncLifetime
{
    private TestDb _db = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _db = await TestDb.CreateAsync();

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task SeedAsync(params Product[] products)
    {
        await using var context = _db.CreateDbContext();
        context.Products.AddRange(products);
        await context.SaveChangesAsync(Ct);
    }

    private async Task<ProductPage> SearchAsync(string? text, bool includeInactive = false)
    {
        await using var context = _db.CreateDbContext();
        var result = await new SearchProductsHandler(new ProductRepository(context))
            .HandleAsync(new SearchProductsQuery(text, includeInactive), Ct);
        return result.Value;
    }

    private static Product P(string name, string sku, string? barcode = null) =>
        Product.Create(name, sku, barcode, Money.FromCents(100), "H87");

    [Fact]
    public async Task NombreSinAcentosNiMayusculas_EncuentraElProducto()
    {
        await SeedAsync(P("Café Molido", "CAF-1"), P("Té Verde", "TE-1"));

        var result = await SearchAsync("cafe molido");

        Assert.Equal(["Café Molido"], result.Items.Select(i => i.Name));
    }

    [Fact]
    public async Task NombreParcialConAcentoEnLaBusqueda_EncuentraElProducto()
    {
        await SeedAsync(P("Cafe Molido", "CAF-1"));

        Assert.Single((await SearchAsync("CAFÉ")).Items);
    }

    [Fact]
    public async Task SkuEnMinusculas_EncuentraElProducto()
    {
        await SeedAsync(P("Leche", "LEC-001"), P("Pan", "PAN-001"));

        Assert.Equal(["LEC-001"], (await SearchAsync("lec-001")).Items.Select(i => i.Sku));
    }

    [Fact]
    public async Task CodigoDeBarrasCompleto_EsExacto_YParcial_EncuentraVarios()
    {
        await SeedAsync(P("Uno", "A-1", "7501234567890"), P("Dos", "B-1", "7501234567891"));

        Assert.Equal(["A-1"], (await SearchAsync("7501234567890")).Items.Select(i => i.Sku));
        Assert.Equal(2, (await SearchAsync("7501")).Items.Count);
    }

    [Fact]
    public async Task Borrados_NuncaAparecen()
    {
        var deleted = P("Borrado", "DEL-1");
        deleted.Delete(_db.Clock.UtcNow);
        await SeedAsync(deleted, P("Vigente", "VIG-1"));

        Assert.Equal(["VIG-1"], (await SearchAsync(null, includeInactive: true)).Items.Select(i => i.Sku));
        Assert.Empty((await SearchAsync("borrado", includeInactive: true)).Items);
    }

    [Fact]
    public async Task Inactivos_SoloAparecenConElFiltro()
    {
        var inactive = P("Inactivo", "INA-1");
        inactive.Update(inactive.Name, inactive.Sku, null, inactive.Price, "H87", isActive: false);
        await SeedAsync(inactive, P("Activo", "ACT-1"));

        Assert.Equal(["ACT-1"], (await SearchAsync(null)).Items.Select(i => i.Sku));
        Assert.Equal(["ACT-1", "INA-1"], (await SearchAsync(null, includeInactive: true)).Items.Select(i => i.Sku));
    }

    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("\\")]
    public async Task ComodinesDeLike_SeBuscanLiteralmente(string wildcard)
    {
        await SeedAsync(P($"Descuento 10{wildcard}", "D-1"), P("Normal", "N-1"));

        var result = await SearchAsync(wildcard);

        Assert.Equal(["D-1"], result.Items.Select(i => i.Sku));
    }

    [Fact]
    public async Task Resultados_OrdenadosPorNombreNormalizado()
    {
        await SeedAsync(P("zanahoria", "Z-1"), P("Álamo", "A-1"), P("berenjena", "B-1"));

        Assert.Equal(["Álamo", "berenjena", "zanahoria"], (await SearchAsync(null)).Items.Select(i => i.Name));
    }

    [Fact]
    public async Task MasDe100Coincidencias_DevuelveLaPrimeraPaginaYElTotal()
    {
        await SeedAsync([.. Enumerable.Range(1, 205).Select(i => P($"Producto {i:000}", $"P-{i:000}"))]);

        var result = await SearchAsync("producto");

        Assert.Equal(100, result.Items.Count);
        Assert.Equal(205, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(1, (await SearchAsync("producto 001")).TotalPages);
    }

    [Fact]
    public async Task SinCoincidencias_DevuelveListaVacia()
    {
        await SeedAsync(P("Leche", "LEC-1"));

        var result = await SearchAsync("inexistente");

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
    }
}
