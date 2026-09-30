using Pos.Application.Products;
using Pos.Application.Products.CreateProduct;
using Pos.Application.Products.DeleteProduct;
using Pos.Application.Products.SearchProducts;
using Pos.Infrastructure.Products;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Products;

/// <summary>Borrado lógico de punta a punta sobre SQLite real (FR-021 y FR-022).</summary>
public sealed class ProductSoftDeleteTests : IAsyncLifetime
{
    private TestDb _db = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _db = await TestDb.CreateAsync();

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<ProductDto> CreateAsync(string name, string sku, string? barcode)
    {
        await using var context = _db.CreateDbContext();
        var result = await new CreateProductHandler(new ProductRepository(context), new CreateProductValidator())
            .HandleAsync(new CreateProductCommand(name, sku, barcode, "10.00", "H87"), Ct);
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value;
    }

    private async Task DeleteAsync(ProductDto product)
    {
        await using var context = _db.CreateDbContext();
        var result = await new DeleteProductHandler(new ProductRepository(context), _db.Clock)
            .HandleAsync(new DeleteProductCommand(product.Id, product.Version), Ct);
        Assert.True(result.IsSuccess, result.Error?.ToString());
    }

    [Fact]
    public async Task Borrar_ConservaLaFilaConFechaYAuditoria_YDesapareceDeLaBusqueda()
    {
        var product = await CreateAsync("Café", "CAF-001", "7501234567890");
        _db.Clock.Advance(TimeSpan.FromMinutes(5));

        await DeleteAsync(product);

        var rows = DatabaseTestHelpers.ReadProductRows(_db.Directory.Paths.DatabaseFile);
        var row = Assert.Single(rows);
        Assert.Contains("2026-09-29 15:35:00", row, StringComparison.Ordinal);

        await using var context = _db.CreateDbContext();
        var repository = new ProductRepository(context);
        Assert.Null(await repository.GetAsync(product.Id, includeImage: false, Ct));
        var search = await new SearchProductsHandler(repository).HandleAsync(new SearchProductsQuery(null, IncludeInactive: true), Ct);
        Assert.Empty(search.Value.Items);
    }

    [Fact]
    public async Task Borrar_RegistraFechaYUsuarioDeUltimaModificacion()
    {
        var product = await CreateAsync("Café", "CAF-001", null);
        _db.Clock.Advance(TimeSpan.FromMinutes(5));

        await DeleteAsync(product);

        await using var context = _db.CreateDbContext();
        var stored = context.Products.Single();
        Assert.Equal(_db.Clock.UtcNow, stored.UpdatedAt);
        Assert.Equal(_db.User.UserId, stored.UpdatedBy);
        Assert.Equal(_db.Clock.UtcNow, stored.DeletedAt);
    }

    [Fact]
    public async Task ProductoNuevoConElSkuYCodigoDeUnoBorrado_SeAcepta()
    {
        var deleted = await CreateAsync("Viejo", "LEC-001", "7501234567890");
        await DeleteAsync(deleted);

        var created = await CreateAsync("Nuevo", "LEC-001", "7501234567890");

        Assert.NotEqual(deleted.Id, created.Id);
        Assert.Equal(2, DatabaseTestHelpers.ReadProductRows(_db.Directory.Paths.DatabaseFile).Count);
    }
}
