using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Abstractions;
using Pos.Application.Products;
using Pos.Application.Products.CreateProduct;
using Pos.Application.Sales;
using Pos.Application.Sales.FindProductsForSale;
using Pos.Domain.Products;
using Pos.Infrastructure.Audit;
using Pos.Infrastructure.Categories;
using Pos.Infrastructure.Inventory;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Products;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Products;

/// <summary>Códigos de barras de CODE128/CODE39 en el catálogo y en la venta sobre SQLite real (021).</summary>
public sealed class BarcodeUseCaseTests : IAsyncLifetime
{
    private TestDb _db = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _db = await TestDb.CreateAsync();

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<Result<ProductDto>> CreateAsync(string name, string sku, string? barcode)
    {
        await using var context = _db.CreateDbContext();
        return await new CreateProductHandler(
                new AllowAllAccessControl(),
                new ProductRepository(context),
                new CreateProductValidator(),
                new CategoryRepository(context),
                new WriteTransactions(context),
                new AuditLog(context),
                NullLogger<CreateProductHandler>.Instance)
            .HandleAsync(new CreateProductCommand(name, sku, barcode, "10.00", "H87"), Ct);
    }

    private async Task<ProductLookup> FindAsync(FindProductsForSaleQuery query)
    {
        await using var context = _db.CreateDbContext();
        var result = await new FindProductsForSaleHandler(new AllowAllAccessControl(), new ProductRepository(context), new InventoryRepository(context))
            .HandleAsync(query, Ct);
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value;
    }

    [Fact]
    public async Task CodigoQueSoloCambiaEnMayusculas_EsDuplicado()
    {
        var first = await CreateAsync("Primero", "SKU-1", "abc-1");
        Assert.True(first.IsSuccess, first.Error?.ToString());
        Assert.Equal("ABC-1", first.Value.Barcode);

        var second = await CreateAsync("Segundo", "SKU-2", "ABC-1");

        Assert.Equal(new Duplicate(ProductFields.Barcode), second.Error);
    }

    [Fact]
    public async Task EscaneoEnMinusculas_EncuentraElCodigoAlfanumerico()
    {
        var product = await CreateAsync("Tornillo", "TOR-1", "ABC-12345");
        Assert.True(product.IsSuccess, product.Error?.ToString());

        var lookup = await FindAsync(new FindProductsForSaleQuery("abc-12345", FromScanner: true));

        Assert.Equal(LookupKind.ExactMatch, lookup.Kind);
        Assert.Equal(product.Value.Id, Assert.Single(lookup.Items).Id);
    }

    [Fact]
    public async Task EscaneoDesconocido_NoBuscaPorNombre()
    {
        Assert.True((await CreateAsync("Leche ZZZ-9990 entera", "LEC-1", null)).IsSuccess);

        var lookup = await FindAsync(new FindProductsForSaleQuery("ZZZ-999", FromScanner: true));

        Assert.Equal(LookupKind.None, lookup.Kind);
        Assert.Equal(BarcodeFormat.Code128OrCode39, lookup.Format);
        Assert.Empty(lookup.Items);
    }
}
