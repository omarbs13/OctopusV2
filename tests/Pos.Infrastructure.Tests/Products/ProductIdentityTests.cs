using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Abstractions;
using Pos.Application.Products;
using Pos.Application.Products.CreateProduct;
using Pos.Application.Products.UpdateProduct;
using Pos.Domain.Suppliers;
using Pos.Infrastructure.Audit;
using Pos.Infrastructure.Categories;
using Pos.Infrastructure.Inventory;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Products;
using Pos.Infrastructure.Suppliers;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Products;

/// <summary>
/// Prueba obligatoria (Principio VI, 026): un producto no puede repetir proveedor + modelo + medidas normalizados,
/// sobre SQLite real y con el índice único como última defensa.
/// </summary>
public sealed class ProductIdentityTests : IAsyncLifetime
{
    private TestDb _db = null!;
    private Guid _supplierA;
    private Guid _supplierB;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDb.CreateAsync();
        _supplierA = await SeedSupplierAsync("Proveedor A");
        _supplierB = await SeedSupplierAsync("Proveedor B");
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<Guid> SeedSupplierAsync(string name)
    {
        var supplier = Supplier.Create(name, null, null, null, null, PaymentTerms.Cash, null);
        await using var context = _db.CreateDbContext();
        context.Suppliers.Add(supplier);
        await context.SaveChangesAsync(Ct);
        return supplier.Id;
    }

    private async Task<Result<ProductDto>> CreateAsync(string model, string? measures, Guid? supplier, string? sku = null)
    {
        await using var context = _db.CreateDbContext();
        return await new CreateProductHandler(
                new AllowAllAccessControl(),
                new ProductRepository(context),
                new CreateProductValidator(),
                new CategoryRepository(context),
                new SupplierRepository(context),
                new WriteTransactions(context),
                new AuditLog(context),
                NullLogger<CreateProductHandler>.Instance)
            .HandleAsync(
                new CreateProductCommand(model, sku, null, "XBX", PricePerSqmText: "175.03", PricePerBoxText: "287.05", PackagingText: "1.64", Measures: measures, SupplierId: supplier),
                Ct);
    }

    private static void AssertDuplicate(Result<ProductDto> result, string expectedSku)
    {
        var duplicate = Assert.IsType<Duplicate>(result.Error);
        Assert.Equal(ProductFields.Identity, duplicate.Field);
        Assert.Equal(expectedSku, duplicate.ExistingSku);
        Assert.NotNull(duplicate.ExistingName);
    }

    [Fact]
    public async Task MismoProveedorModeloYMedida_SeRechazaConElProductoExistente()
    {
        var first = await CreateAsync("SUNWOOD MIEL", "18X60", _supplierA, "SW-1");
        Assert.True(first.IsSuccess, first.Error?.ToString());

        AssertDuplicate(await CreateAsync("SUNWOOD MIEL", "18X60", _supplierA), "SW-1");
    }

    [Fact]
    public async Task VariantesNormalizadas_SeRechazan()
    {
        Assert.True((await CreateAsync("SUNWOOD MIEL", "18X60", _supplierA, "SW-1")).IsSuccess);
        Assert.True((await CreateAsync("Piedra", "120X240", _supplierA, "PI-1")).IsSuccess);
        Assert.True((await CreateAsync("Cerámica", "33 X 33", _supplierA, "CE-1")).IsSuccess);

        AssertDuplicate(await CreateAsync("sunwood  miel", "18 x 60", _supplierA), "SW-1");
        AssertDuplicate(await CreateAsync("PIEDRA", "120*240", _supplierA), "PI-1");
        AssertDuplicate(await CreateAsync("Ceramica", "33x33", _supplierA), "CE-1");
    }

    [Fact]
    public async Task OtraMedida_SeAcepta()
    {
        Assert.True((await CreateAsync("SUNWOOD MIEL", "18X60", _supplierA)).IsSuccess);

        Assert.True((await CreateAsync("SUNWOOD MIEL", "20X90", _supplierA)).IsSuccess);
    }

    [Fact]
    public async Task MismoModeloYMedidaConOtroProveedor_SeAcepta()
    {
        Assert.True((await CreateAsync("SUNWOOD MIEL", "18X60", _supplierA)).IsSuccess);

        Assert.True((await CreateAsync("SUNWOOD MIEL", "18X60", _supplierB)).IsSuccess);
    }

    [Fact]
    public async Task SinProveedor_SoloChocaConOtroSinProveedor()
    {
        Assert.True((await CreateAsync("SUNWOOD MIEL", "18X60", null, "NS-1")).IsSuccess);

        Assert.True((await CreateAsync("SUNWOOD MIEL", "18X60", _supplierA)).IsSuccess);
        AssertDuplicate(await CreateAsync("sunwood miel", "18X60", null), "NS-1");
    }

    [Fact]
    public async Task EditarOtroProductoHastaRepetirLaIdentidad_SeRechaza()
    {
        Assert.True((await CreateAsync("SUNWOOD MIEL", "18X60", _supplierA, "SW-1")).IsSuccess);
        var other = (await CreateAsync("SUNWOOD MIEL", "20X90", _supplierA, "SW-2")).Value;
        await using var context = _db.CreateDbContext();

        var result = await new UpdateProductHandler(
                new AllowAllAccessControl(),
                new ProductRepository(context),
                new UpdateProductValidator(),
                new InventoryRepository(context),
                new WriteTransactions(context),
                new CategoryRepository(context),
                new SupplierRepository(context),
                new AuditLog(context),
                NullLogger<UpdateProductHandler>.Instance)
            .HandleAsync(
                new UpdateProductCommand(
                    other.Id, other.Version, "SUNWOOD MIEL", other.Sku, null, "XBX", true, PricePerSqmText: "175.03", PricePerBoxText: "287.05",
                    PackagingText: "1.64", Measures: "18 x 60", SupplierId: _supplierA),
                Ct);

        AssertDuplicate(result, "SW-1");
    }

    [Fact]
    public async Task ProductoBorrado_LiberaSuIdentidad()
    {
        var first = (await CreateAsync("SUNWOOD MIEL", "18X60", _supplierA)).Value;
        await using (var context = _db.CreateDbContext())
        {
            var product = await context.Products.SingleAsync(p => p.Id == first.Id, Ct);
            product.Delete(DateTime.UtcNow);
            await context.SaveChangesAsync(Ct);
        }

        Assert.True((await CreateAsync("SUNWOOD MIEL", "18X60", _supplierA)).IsSuccess);
    }

    [Fact]
    public async Task ElIndiceUnico_ImpideLaCarrera()
    {
        var first = (await CreateAsync("SUNWOOD MIEL", "18X60", _supplierA, "SW-1")).Value;
        await using var context = _db.CreateDbContext();
        var repository = new ProductRepository(context);
        var clone = Pos.Domain.Products.Product.Create(
            new Pos.Domain.Products.ProductDetails("SUNWOOD MIEL", "18X60", SupplierId: _supplierA, PricePerBoxCents: 100),
            "SW-2",
            null,
            "XBX");
        repository.Add(clone);

        var outcome = await repository.SaveChangesAsync(clone, expectedVersion: null, Ct);

        Assert.Equal(SaveStatus.Duplicate, outcome.Status);
        Assert.Equal(ProductFields.Identity, outcome.DuplicateField);
        Assert.NotNull(first);
    }

    [Fact]
    public async Task SkuVacio_GeneraUnoUnico()
    {
        var first = await CreateAsync("Uno", null, _supplierA);
        var second = await CreateAsync("Dos", null, _supplierA);

        Assert.True(first.IsSuccess && second.IsSuccess);
        Assert.NotEqual(first.Value.Sku, second.Value.Sku);
        Assert.StartsWith("P-", first.Value.Sku, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EditarUnDuplicadoMigradoHastaQuedarUnico_QuitaLaMarcaYCalculaLaClave()
    {
        var product = (await CreateAsync("Repetido", "30X30", _supplierA, "RE-1")).Value;
        await using (var context = _db.CreateDbContext())
        {
            // Como lo deja la migración: marcado para revisión y sin clave de identidad.
            await context.Products.Where(p => p.Id == product.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.NeedsReview, true).SetProperty(p => p.IdentityKey, (string?)null), Ct);
        }

        await using var update = _db.CreateDbContext();
        var result = await new UpdateProductHandler(
                new AllowAllAccessControl(),
                new ProductRepository(update),
                new UpdateProductValidator(),
                new InventoryRepository(update),
                new WriteTransactions(update),
                new CategoryRepository(update),
                new SupplierRepository(update),
                new AuditLog(update),
                NullLogger<UpdateProductHandler>.Instance)
            .HandleAsync(
                new UpdateProductCommand(
                    product.Id, product.Version, "Repetido", product.Sku, null, "XBX", true, PricePerSqmText: "175.03", PricePerBoxText: "287.05",
                    PackagingText: "1.64", Measures: "30X40", SupplierId: _supplierA),
                Ct);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.False(result.Value.NeedsReview);
        await using var check = _db.CreateDbContext();
        Assert.NotNull((await check.Products.AsNoTracking().SingleAsync(p => p.Id == product.Id, Ct)).IdentityKey);
    }
}
