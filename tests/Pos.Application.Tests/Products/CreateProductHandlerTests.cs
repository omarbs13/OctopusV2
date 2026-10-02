using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Abstractions;
using Pos.Application.Products;
using Pos.Application.Products.CreateProduct;
using Pos.Application.Tests.TestSupport;
using Pos.Domain.Common;
using Pos.Domain.Products;

namespace Pos.Application.Tests.Products;

public class CreateProductHandlerTests
{
    private readonly InMemoryProductRepository _repository = new();

    private CreateProductHandler Handler => new(
        new AllowAllAccessControl(),
        _repository,
        new CreateProductValidator(),
        new InMemoryCategoryRepository(),
        new FakeWriteTransactions(),
        NullLogger<CreateProductHandler>.Instance);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ComandoValido_CreaProductoActivoNormalizado()
    {
        var result = await Handler.HandleAsync(new CreateProductCommand("  Café Molido ", "caf-001", "7501234567890", "89.5", "H87"), Ct);

        Assert.True(result.IsSuccess);
        var dto = result.Value;
        Assert.Equal("Café Molido", dto.Name);
        Assert.Equal("CAF-001", dto.Sku);
        Assert.Equal("7501234567890", dto.Barcode);
        Assert.Equal(8950, dto.PriceCents);
        Assert.True(dto.IsActive);
        Assert.Equal(1, dto.Version);
        Assert.Single(_repository.All);
    }

    [Fact]
    public async Task DatosInvalidos_DevuelveValidationFailedSinGuardar()
    {
        var result = await Handler.HandleAsync(new CreateProductCommand("", "SKU1", null, "12,50", "H87"), Ct);

        var error = Assert.IsType<ValidationFailed>(result.Error);
        Assert.Equal([ProductFields.Name, ProductFields.Price], error.Errors.Select(e => e.Field));
        Assert.Equal(0, _repository.AddCount);
    }

    [Fact]
    public async Task SkuExistenteSinDistinguirMayusculas_DevuelveDuplicateSku()
    {
        _repository.Seed(Product.Create("Otro", "ABC-1", null, Money.FromCents(100), "H87"));

        var result = await Handler.HandleAsync(new CreateProductCommand("Nuevo", "abc-1", null, "10", "H87"), Ct);

        Assert.Equal(new Duplicate(ProductFields.Sku), result.Error);
        Assert.Equal(0, _repository.AddCount);
    }

    [Fact]
    public async Task CodigoDeBarrasExistente_DevuelveDuplicateBarcode()
    {
        _repository.Seed(Product.Create("Otro", "ABC-1", "7501234567890", Money.FromCents(100), "H87"));

        var result = await Handler.HandleAsync(new CreateProductCommand("Nuevo", "XYZ-1", "7501234567890", "10", "H87"), Ct);

        Assert.Equal(new Duplicate(ProductFields.Barcode), result.Error);
    }

    [Fact]
    public async Task DuplicadoDetectadoAlGuardar_DevuelveDuplicate()
    {
        _repository.NextOutcome = SaveOutcome.Duplicate(ProductFields.Sku);

        var result = await Handler.HandleAsync(new CreateProductCommand("Nuevo", "XYZ-1", null, "10", "H87"), Ct);

        Assert.Equal(new Duplicate(ProductFields.Sku), result.Error);
    }
}
