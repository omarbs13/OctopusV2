using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Products;
using Pos.Application.Products.CreateProduct;
using Pos.Application.Products.UpdateProduct;
using Pos.Application.Reports.SetProductCritical;
using Pos.Domain.Audit;
using Pos.Domain.Categories;
using Pos.Infrastructure.Audit;
using Pos.Infrastructure.Categories;
using Pos.Infrastructure.Inventory;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Products;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Audit;

/// <summary>
/// 018 (FR-001, FR-002, FR-012) sobre SQLite real: la modificación de un producto queda en la bitácora con
/// solo los campos cambiados, en el mismo guardado; sin cambios o con conflicto no queda nada.
/// </summary>
public sealed class ProductAuditTests : IAsyncLifetime
{
    private TestDb _db = null!;
    private Category _drinks = null!;
    private Category _sodas = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDb.CreateAsync();
        _drinks = Category.Create("Bebidas", null);
        _sodas = Category.Create("Refrescos", null);
        await using var context = _db.CreateDbContext();
        context.Categories.AddRange(_drinks, _sodas);
        await context.SaveChangesAsync(Ct);
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task CambioDePrecioYCategoria_UnaEntradaConExactamenteEsosDosCambios()
    {
        var product = await CreateAsync();

        var updated = await UpdateAsync(product, "28.50", _sodas.Id, product.Version);

        Assert.True(updated.IsSuccess, updated.Error?.ToString());
        var entry = Assert.Single(await EntriesAsync(AuditActions.ProductUpdated));
        Assert.Equal(product.Id, entry.EntityId);
        Assert.Equal("Coca-Cola 600 ml", entry.EntityName);
        Assert.Equal(
            [("Precio", "$25.00", "$28.50"), ("Categoría", "Bebidas", "Refrescos")],
            entry.Changes.Select(c => (c.Field, c.Before, c.After)));
    }

    [Fact]
    public async Task GuardarSinCambios_NoCreaEntrada()
    {
        var product = await CreateAsync();

        Assert.True((await UpdateAsync(product, "25.00", _drinks.Id, product.Version)).IsSuccess);

        Assert.Empty(await EntriesAsync(AuditActions.ProductUpdated));
    }

    [Fact]
    public async Task ConflictoDeVersion_NoDejaEntrada()
    {
        var product = await CreateAsync();
        Assert.True((await UpdateAsync(product, "26.00", _drinks.Id, product.Version)).IsSuccess);

        // Otro operador ya guardó: la versión que se vio es la anterior.
        var stale = await UpdateAsync(product, "30.00", _drinks.Id, product.Version);

        Assert.IsType<Conflict>(stale.Error);
        var entry = Assert.Single(await EntriesAsync(AuditActions.ProductUpdated));
        Assert.Equal("$26.00", Assert.Single(entry.Changes).After);
    }

    [Fact]
    public async Task MarcarComoCritico_RegistraElUnicoCambioCritico()
    {
        var product = await CreateAsync();

        await using (var context = _db.CreateDbContext())
        {
            var result = await new SetProductCriticalHandler(new AllowAllAccessControl(), new ProductRepository(context), new AuditLog(context))
                .HandleAsync(new SetProductCriticalCommand(product.Id, true), Ct);
            Assert.True(result.IsSuccess, result.Error?.ToString());
        }

        var entry = Assert.Single(await EntriesAsync(AuditActions.ProductUpdated));
        var change = Assert.Single(entry.Changes);
        Assert.Equal(("Crítico", "No", "Sí"), (change.Field, change.Before, change.After));
    }

    private async Task<ProductDto> CreateAsync()
    {
        await using var context = _db.CreateDbContext();
        var result = await new CreateProductHandler(
                new AllowAllAccessControl(),
                new ProductRepository(context),
                new CreateProductValidator(),
                new CategoryRepository(context),
                new WriteTransactions(context),
                new AuditLog(context),
                NullLogger<CreateProductHandler>.Instance)
            .HandleAsync(new CreateProductCommand("Coca-Cola 600 ml", "COCA-600", null, "25.00", "H87", CategoryId: _drinks.Id), Ct);
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value;
    }

    private async Task<Result<ProductDto>> UpdateAsync(ProductDto product, string price, Guid categoryId, int version)
    {
        await using var context = _db.CreateDbContext();
        return await new UpdateProductHandler(
                new AllowAllAccessControl(),
                new ProductRepository(context),
                new UpdateProductValidator(),
                new InventoryRepository(context),
                new WriteTransactions(context),
                new CategoryRepository(context),
                new AuditLog(context),
                NullLogger<UpdateProductHandler>.Instance)
            .HandleAsync(
                new UpdateProductCommand(product.Id, version, product.Name, product.Sku, product.Barcode, price, product.UnitCode, true, CategoryId: categoryId),
                Ct);
    }

    private async Task<List<AuditEntry>> EntriesAsync(string action)
    {
        await using var context = _db.CreateDbContext();
        return await context.AuditEntries.AsNoTracking().Where(e => e.Action == action).ToListAsync(Ct);
    }
}
