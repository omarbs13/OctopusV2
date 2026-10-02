using Pos.Application.Abstractions;
using Pos.Application.Products.DeleteProduct;
using Pos.Application.Tests.TestSupport;
using Pos.Domain.Common;
using Pos.Domain.Products;

namespace Pos.Application.Tests.Products;

public class DeleteProductHandlerTests
{
    private readonly InMemoryProductRepository _repository = new();
    private readonly FakeClock _clock = new();

    private DeleteProductHandler Handler => new(new AllowAllAccessControl(), _repository, _clock, new InMemoryCategoryRepository(), new RecordingAuditLog());

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Product Seed() => _repository.Seed(Product.Create("Café", "CAF-001", null, Money.FromCents(100), "H87"));

    [Fact]
    public async Task Borrar_AsignaLaFechaDeBorradoDelReloj()
    {
        var product = Seed();

        var result = await Handler.HandleAsync(new DeleteProductCommand(product.Id, product.Version), Ct);

        Assert.True(result.IsSuccess);
        var stored = _repository.All.Single();
        Assert.True(stored.IsDeleted);
        Assert.Equal(_clock.UtcNow, stored.DeletedAt);
    }

    [Fact]
    public async Task ProductoInexistente_DevuelveNotFound()
    {
        var result = await Handler.HandleAsync(new DeleteProductCommand(Guid.CreateVersion7(), 1), Ct);

        Assert.IsType<NotFound>(result.Error);
    }

    [Fact]
    public async Task ProductoYaBorrado_DevuelveNotFound()
    {
        var product = Seed();
        await Handler.HandleAsync(new DeleteProductCommand(product.Id, product.Version), Ct);

        var result = await Handler.HandleAsync(new DeleteProductCommand(product.Id, product.Version + 1), Ct);

        Assert.IsType<NotFound>(result.Error);
    }

    [Fact]
    public async Task VersionDesactualizada_DevuelveConflictSinBorrar()
    {
        var product = Seed();
        _repository.BumpVersion(product.Id);

        var result = await Handler.HandleAsync(new DeleteProductCommand(product.Id, product.Version), Ct);

        Assert.IsType<Conflict>(result.Error);
        Assert.False(_repository.All.Single().IsDeleted);
    }
}
