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
    private readonly InMemorySupplierRepository _suppliers = new();

    private CreateProductHandler Handler => new(
        new AllowAllAccessControl(),
        _repository,
        new CreateProductValidator(),
        new InMemoryCategoryRepository(),
        _suppliers,
        new FakeWriteTransactions(),
        new RecordingAuditLog(),
        NullLogger<CreateProductHandler>.Instance);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ComandoValido_CreaProductoActivoNormalizado()
    {
        var result = await Handler.HandleAsync(new CreateProductCommand("  Café Molido ", "caf-001", "7501234567890", "H87", "89.5"), Ct);

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
        var result = await Handler.HandleAsync(new CreateProductCommand("", "SKU1", null, "H87", "12,50"), Ct);

        var error = Assert.IsType<ValidationFailed>(result.Error);
        Assert.Equal([ProductFields.Model, ProductFields.Price], error.Errors.Select(e => e.Field));
        Assert.Equal(0, _repository.AddCount);
    }

    [Fact]
    public async Task SkuExistenteSinDistinguirMayusculas_DevuelveDuplicateSku()
    {
        _repository.Seed(Product.Create("Otro", "ABC-1", null, Money.FromCents(100), "H87"));

        var result = await Handler.HandleAsync(new CreateProductCommand("Nuevo", "abc-1", null, "H87", "10"), Ct);

        Assert.Equal(new Duplicate(ProductFields.Sku), result.Error);
        Assert.Equal(0, _repository.AddCount);
    }

    [Fact]
    public async Task CodigoDeBarrasExistente_DevuelveDuplicateBarcode()
    {
        _repository.Seed(Product.Create("Otro", "ABC-1", "7501234567890", Money.FromCents(100), "H87"));

        var result = await Handler.HandleAsync(new CreateProductCommand("Nuevo", "XYZ-1", "7501234567890", "H87", "10"), Ct);

        Assert.Equal(new Duplicate(ProductFields.Barcode), result.Error);
    }

    [Fact]
    public async Task DuplicadoDetectadoAlGuardar_DevuelveDuplicate()
    {
        _repository.NextOutcome = SaveOutcome.Duplicate(ProductFields.Sku);

        var result = await Handler.HandleAsync(new CreateProductCommand("Nuevo", "XYZ-1", null, "H87", "10"), Ct);

        Assert.Equal(new Duplicate(ProductFields.Sku), result.Error);
    }

    private static CreateProductCommand Tile(string model = "Piedra", string? sku = "PI-1", string? sqm = "175.03", string? box = "287.05", string? packaging = "1.64") =>
        new(model, sku, null, "XBX", PricePerSqmText: sqm, PricePerBoxText: box, PackagingText: packaging, Measures: "33 X 33");

    [Fact]
    public async Task ModeloVacio_SeRechazaPorCampo()
    {
        var result = await Handler.HandleAsync(Tile(model: "  "), Ct);

        var error = Assert.IsType<ValidationFailed>(result.Error);
        Assert.Equal(ProductFields.Model, Assert.Single(error.Errors).Field);
    }

    [Fact]
    public async Task LosetaSinPrecios_SeRechazaEnElCampoDeXCaja()
    {
        var result = await Handler.HandleAsync(Tile(sqm: null, box: null), Ct);

        var error = Assert.IsType<ValidationFailed>(result.Error);
        Assert.Equal((ProductFields.PricePerBox, ProductMessages.TilePriceRequired), (error.Errors[0].Field, error.Errors[0].Message));
        Assert.Equal(0, _repository.AddCount);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("1.8888")]
    public async Task EmbalajeInvalido_SeRechaza(string packaging)
    {
        var result = await Handler.HandleAsync(Tile(packaging: packaging), Ct);

        var error = Assert.IsType<ValidationFailed>(result.Error);
        Assert.Equal(ProductFields.Packaging, Assert.Single(error.Errors).Field);
    }

    [Theory]
    [InlineData("NA")]
    [InlineData("na")]
    [InlineData("")]
    [InlineData(null)]
    public async Task EmbalajeNaOVacio_NoAplica(string? packaging)
    {
        var result = await Handler.HandleAsync(Tile(packaging: packaging), Ct);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.Null(result.Value.PackagingThousandths);
    }

    [Fact]
    public async Task Embalaje18Y180_SonElMismoValor()
    {
        var result = await Handler.HandleAsync(Tile(packaging: "1.8"), Ct);

        Assert.Equal(1800, result.Value.PackagingThousandths);
    }

    [Fact]
    public async Task SkuVacio_GeneraUnoUnicoQueNoChocaConLosExistentes()
    {
        var first = await Handler.HandleAsync(Tile(model: "Uno", sku: null), Ct);
        var second = await Handler.HandleAsync(Tile(model: "Dos", sku: "  "), Ct);

        Assert.True(first.IsSuccess && second.IsSuccess);
        Assert.NotEqual(first.Value.Sku, second.Value.Sku);
    }

    [Fact]
    public async Task DiferenciaMayorAl2Por100_AdvierteSinBloquear()
    {
        // X M2 175.03 × 1.64 = 287.05; X CAJA 300.00 se aleja más del 2 %.
        var result = await Handler.HandleAsync(Tile(box: "300.00"), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal([ProductMessages.PriceDifference], result.Value.Warnings);
    }

    [Fact]
    public async Task DiferenciaDentroDelDosPorCiento_NoAdvierte()
    {
        var result = await Handler.HandleAsync(Tile(), Ct);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Warnings ?? []);
    }

    [Fact]
    public async Task ProductoQueNoEsLoseta_RequierePrecioPorPieza()
    {
        var result = await Handler.HandleAsync(new CreateProductCommand("Café", "CAF-1", null, "H87"), Ct);

        var error = Assert.IsType<ValidationFailed>(result.Error);
        Assert.Equal((ProductFields.Price, ProductMessages.PriceRequired), (error.Errors[0].Field, error.Errors[0].Message));
    }

    [Fact]
    public async Task ProveedorInexistenteOInactivo_SeRechaza()
    {
        var inactive = _suppliers.Seed(Pos.Domain.Suppliers.Supplier.Create("Inactivo", null, null, null, null, Pos.Domain.Suppliers.PaymentTerms.Cash, null));
        inactive.Deactivate();

        var missing = await Handler.HandleAsync(Tile() with { SupplierId = Guid.CreateVersion7() }, Ct);
        var disabled = await Handler.HandleAsync(Tile() with { SupplierId = inactive.Id }, Ct);

        Assert.Equal(ProductFields.Supplier, Assert.IsType<ValidationFailed>(missing.Error).Errors[0].Field);
        Assert.Equal(ProductFields.Supplier, Assert.IsType<ValidationFailed>(disabled.Error).Errors[0].Field);
    }

    [Fact]
    public async Task IdentidadRepetida_DevuelveElProductoExistente()
    {
        Assert.True((await Handler.HandleAsync(Tile(sku: "PI-1"), Ct)).IsSuccess);

        var result = await Handler.HandleAsync(Tile(model: "PIEDRA", sku: "PI-2") with { Measures = "33x33" }, Ct);

        var duplicate = Assert.IsType<Duplicate>(result.Error);
        Assert.Equal((ProductFields.Identity, "PI-1"), (duplicate.Field, duplicate.ExistingSku));
    }
}
