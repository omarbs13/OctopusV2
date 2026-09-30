using Pos.Application.Products.CountActiveProducts;
using Pos.Application.Tests.TestSupport;
using Pos.Domain.Common;
using Pos.Domain.Products;

namespace Pos.Application.Tests.Products;

public class CountActiveProductsHandlerTests
{
    private readonly InMemoryProductRepository _repository = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CuentaSoloActivosNoBorrados()
    {
        for (var i = 0; i < 12; i++)
        {
            _repository.Seed(Product.Create($"Activo {i}", $"A-{i}", null, Money.FromCents(100), "H87"));
        }

        for (var i = 0; i < 3; i++)
        {
            var inactive = Product.Create($"Inactivo {i}", $"I-{i}", null, Money.FromCents(100), "H87");
            inactive.Update(inactive.Name, inactive.Sku, null, inactive.Price, "H87", isActive: false);
            _repository.Seed(inactive);
        }

        for (var i = 0; i < 2; i++)
        {
            var deleted = Product.Create($"Borrado {i}", $"B-{i}", null, Money.FromCents(100), "H87");
            deleted.Delete(DateTime.UtcNow);
            _repository.Seed(deleted);
        }

        var result = await new CountActiveProductsHandler(_repository).HandleAsync(Ct);

        Assert.Equal(12, result.Value);
    }

    [Fact]
    public async Task SinProductos_EsCero()
    {
        var result = await new CountActiveProductsHandler(_repository).HandleAsync(Ct);

        Assert.Equal(0, result.Value);
    }
}
