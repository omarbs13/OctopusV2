using Pos.Application.Abstractions;
using Pos.Application.Products.GetProduct;
using Pos.Application.Tests.TestSupport;
using Pos.Domain.Common;
using Pos.Domain.Products;

namespace Pos.Application.Tests.Products;

public class GetProductHandlerTests
{
    private readonly InMemoryProductRepository _repository = new();

    private GetProductHandler Handler => new(_repository);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ProductoExistente_DevuelveTodosSusDatosYVersionActual()
    {
        var product = _repository.Seed(Product.Create("Café", "CAF-001", "7501234567890", Money.FromCents(8950), "H87"));
        _repository.BumpVersion(product.Id);

        var result = await Handler.HandleAsync(new GetProductQuery(product.Id), Ct);

        var dto = result.Value;
        Assert.Equal((product.Id, "Café", "CAF-001", "7501234567890", 8950L, true, 2),
            (dto.Id, dto.Name, dto.Sku, dto.Barcode, dto.PriceCents, dto.IsActive, dto.Version));
    }

    [Fact]
    public async Task ProductoInexistente_DevuelveNotFound()
    {
        var result = await Handler.HandleAsync(new GetProductQuery(Guid.CreateVersion7()), Ct);

        Assert.IsType<NotFound>(result.Error);
    }

    [Fact]
    public async Task ProductoBorrado_DevuelveNotFound()
    {
        var product = Product.Create("Café", "CAF-001", null, Money.FromCents(100), "H87");
        product.Delete(DateTime.UtcNow);
        _repository.Seed(product);

        var result = await Handler.HandleAsync(new GetProductQuery(product.Id), Ct);

        Assert.IsType<NotFound>(result.Error);
    }
}
