using Microsoft.Data.Sqlite;
using Pos.Application.Products;
using Pos.Domain.Common;
using Pos.Domain.Products;
using Pos.Infrastructure.Products;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Products;

public sealed class ProductRepositoryCreateTests : IAsyncLifetime
{
    private TestDb _db = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _db = await TestDb.CreateAsync();

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<SaveOutcome> AddAsync(Product product)
    {
        await using var context = _db.CreateDbContext();
        var repository = new ProductRepository(context);
        repository.Add(product);
        return await repository.SaveChangesAsync(product, null, Ct);
    }

    [Fact]
    public async Task Alta_GuardaYLeeConAuditoriaEnUtc()
    {
        var product = Product.Create("Café Molido", "CAF-001", "7501234567890", Money.FromCents(8950), "H87");

        Assert.Equal(SaveOutcome.Saved, await AddAsync(product));

        await using var context = _db.CreateDbContext();
        var loaded = await new ProductRepository(context).GetAsync(product.Id, includeImage: false, Ct);
        Assert.NotNull(loaded);
        Assert.Equal("Café Molido", loaded.Name);
        Assert.Equal("cafe molido", loaded.NameSearch);
        Assert.Equal(Money.FromCents(8950), loaded.Price);
        Assert.Equal(_db.Clock.UtcNow, loaded.CreatedAt);
        Assert.Equal(_db.Clock.UtcNow, loaded.UpdatedAt);
        Assert.Equal(DateTimeKind.Utc, loaded.CreatedAt.Kind);
        Assert.Equal(_db.User.UserId, loaded.CreatedBy);
        Assert.Equal(_db.User.UserId, loaded.UpdatedBy);
        Assert.Equal(1, loaded.Version);
    }

    [Fact]
    public async Task Precio_SeGuardaComoEnteroEnCentavos()
    {
        var product = Product.Create("Leche", "LEC-001", null, Money.FromCents(123405), "H87");
        await AddAsync(product);

        using var connection = new SqliteConnection($"Data Source={_db.Directory.Paths.DatabaseFile};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT typeof(PriceCents), PriceCents FROM Products";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("integer", reader.GetString(0));
        Assert.Equal(123405, reader.GetInt64(1));
    }

    [Fact]
    public async Task SkuDuplicadoEntreNoBorrados_SeTraduceADuplicateSku()
    {
        await AddAsync(Product.Create("Uno", "DUP-1", null, Money.FromCents(100), "H87"));

        var outcome = await AddAsync(Product.Create("Dos", "DUP-1", null, Money.FromCents(100), "H87"));

        Assert.Equal(SaveOutcome.Duplicate(ProductFields.Sku), outcome);
    }

    [Fact]
    public async Task CodigoDeBarrasDuplicado_SeTraduceADuplicateBarcode()
    {
        await AddAsync(Product.Create("Uno", "A-1", "7501234567890", Money.FromCents(100), "H87"));

        var outcome = await AddAsync(Product.Create("Dos", "B-1", "7501234567890", Money.FromCents(100), "H87"));

        Assert.Equal(SaveOutcome.Duplicate(ProductFields.Barcode), outcome);
    }

    [Fact]
    public async Task VariosProductosSinCodigoDeBarras_NoChocan()
    {
        Assert.Equal(SaveOutcome.Saved, await AddAsync(Product.Create("Uno", "A-1", null, Money.FromCents(100), "H87")));
        Assert.Equal(SaveOutcome.Saved, await AddAsync(Product.Create("Dos", "B-1", null, Money.FromCents(100), "H87")));
    }

    [Fact]
    public async Task ExisteSkuYCodigo_ExcluyenAlPropioProducto()
    {
        var product = Product.Create("Uno", "A-1", "7501234567890", Money.FromCents(100), "H87");
        await AddAsync(product);

        await using var context = _db.CreateDbContext();
        var repository = new ProductRepository(context);

        Assert.True(await repository.SkuExistsAsync("A-1", null, Ct));
        Assert.False(await repository.SkuExistsAsync("A-1", product.Id, Ct));
        Assert.True(await repository.BarcodeExistsAsync("7501234567890", null, Ct));
        Assert.False(await repository.BarcodeExistsAsync("7501234567890", product.Id, Ct));
    }

    [Fact]
    public async Task NombreConAcentosYCaracteresEspeciales_SeConservaSinAlteraciones()
    {
        const string name = "Jalapeño «Extra» 100% & más 'ñ' \"x\"";
        var product = Product.Create(name, "JAL-1", null, Money.FromCents(100), "H87");
        await AddAsync(product);

        await using var context = _db.CreateDbContext();
        var loaded = await new ProductRepository(context).GetAsync(product.Id, includeImage: false, Ct);

        Assert.Equal(name, loaded!.Name);
    }
}
