using Microsoft.EntityFrameworkCore;
using Pos.Application.Products;
using Pos.Domain.Common;
using Pos.Domain.Products;
using Pos.Infrastructure.Products;
using Pos.Infrastructure.Tests.TestSupport;

namespace Pos.Infrastructure.Tests.Products;

/// <summary>Imagen del producto con SQLite real (003, FR-027 y FR-030; SC-004).</summary>
public sealed class ProductImagePersistenceTests : IAsyncLifetime
{
    private TestDb _db = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _db = await TestDb.CreateAsync();

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<Product> CreateWithImageAsync(string sku = "CAF-1")
    {
        await using var context = _db.CreateDbContext();
        var repository = new ProductRepository(context);
        var product = Product.Create("Café", sku, null, Money.FromCents(100), "H87");
        product.SetImage([1, 1, 1], [1], 3, 1);
        repository.Add(product);
        Assert.Equal(SaveStatus.Saved, (await repository.SaveChangesAsync(product, null, Ct)).Status);
        return product;
    }

    private async Task<long> CountImagesAsync()
    {
        await using var context = _db.CreateDbContext();
        return await context.ProductImages.LongCountAsync(Ct);
    }

    private async Task<Product> LoadAsync(Guid id)
    {
        await using var context = _db.CreateDbContext();
        return (await new ProductRepository(context).GetAsync(id, includeImage: true, Ct))!;
    }

    [Fact]
    public async Task Alta_ConImagen_GuardaLaImagenYLaMiniaturaApareceEnElListado()
    {
        var product = await CreateWithImageAsync();

        await using var context = _db.CreateDbContext();
        var page = await new ProductRepository(context)
            .SearchAsync(new ProductSearch(null, null, null, false, false, 1, 100), Ct);

        Assert.Equal([1, 1, 1], (await LoadAsync(product.Id)).Image!.Content);
        Assert.Equal([(byte)1], page.Items.Single().Thumbnail!);
    }

    [Fact]
    public async Task SinIncluirImagen_NoSeCarga()
    {
        var product = await CreateWithImageAsync();

        await using var context = _db.CreateDbContext();
        var loaded = await new ProductRepository(context).GetAsync(product.Id, includeImage: false, Ct);

        Assert.Null(loaded!.Image);
    }

    [Fact]
    public async Task ReemplazarSoloLaImagen_UnaFilaEIncrementaVersionYFecha()
    {
        var created = await CreateWithImageAsync();
        _db.Clock.Advance(TimeSpan.FromMinutes(5));

        await using (var context = _db.CreateDbContext())
        {
            var repository = new ProductRepository(context);
            var product = (await repository.GetAsync(created.Id, includeImage: true, Ct))!;
            product.SetImage([2, 2], [2], 2, 1);
            Assert.Equal(SaveStatus.Saved, (await repository.SaveChangesAsync(product, product.Version, Ct)).Status);
        }

        var stored = await LoadAsync(created.Id);
        Assert.Equal(1, await CountImagesAsync());
        Assert.Equal([2, 2], stored.Image!.Content);
        Assert.Equal(2, stored.Version);
        Assert.Equal(_db.Clock.UtcNow, stored.UpdatedAt);
        Assert.Equal(_db.Clock.UtcNow, stored.Image.UpdatedAt);
    }

    [Fact]
    public async Task QuitarLaImagen_BorraLaFilaEIncrementaVersion()
    {
        var created = await CreateWithImageAsync();

        await using (var context = _db.CreateDbContext())
        {
            var repository = new ProductRepository(context);
            var product = (await repository.GetAsync(created.Id, includeImage: true, Ct))!;
            product.RemoveImage();
            Assert.Equal(SaveStatus.Saved, (await repository.SaveChangesAsync(product, product.Version, Ct)).Status);
        }

        Assert.Equal(0, await CountImagesAsync());
        Assert.Equal(2, (await LoadAsync(created.Id)).Version);
    }

    [Fact]
    public async Task GuardadoFallidoPorSkuDuplicado_NoGuardaNiDatosNiImagen()
    {
        await using (var context = _db.CreateDbContext())
        {
            context.Products.Add(Product.Create("Leche", "LEC-1", null, Money.FromCents(100), "H87"));
            await context.SaveChangesAsync(Ct);
        }

        await using (var context = _db.CreateDbContext())
        {
            var repository = new ProductRepository(context);
            var duplicate = Product.Create("Otra leche", "LEC-1", null, Money.FromCents(100), "H87");
            duplicate.SetImage([3], [3], 1, 1);
            repository.Add(duplicate);

            Assert.Equal(SaveStatus.Duplicate, (await repository.SaveChangesAsync(duplicate, null, Ct)).Status);
        }

        Assert.Equal(0, await CountImagesAsync());
    }

    [Fact]
    public async Task ProductoBorrado_ConservaSuImagen()
    {
        var created = await CreateWithImageAsync();

        await using (var context = _db.CreateDbContext())
        {
            var product = await context.Products.SingleAsync(p => p.Id == created.Id, Ct);
            product.Delete(DateTime.UtcNow);
            await context.SaveChangesAsync(Ct);
        }

        Assert.Equal(1, await CountImagesAsync());
    }
}
