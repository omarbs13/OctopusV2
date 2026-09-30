using Pos.Application.Abstractions;
using Pos.Application.Products;
using Pos.Application.Products.UpdateProduct;
using Pos.Application.Tests.TestSupport;
using Pos.Domain.Common;
using Pos.Domain.Products;

namespace Pos.Application.Tests.Products;

public class UpdateProductHandlerTests
{
    private readonly InMemoryProductRepository _repository = new();

    private readonly InMemoryInventoryRepository _inventory = new();
    private readonly FakeWriteTransactions _transactions = new();

    private UpdateProductHandler Handler => new(_repository, new UpdateProductValidator(), _inventory, _transactions);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Product Seed(string sku = "CAF-001", string? barcode = null) =>
        _repository.Seed(Product.Create("Café", sku, barcode, Money.FromCents(8950), "H87"));

    private static UpdateProductCommand Command(Product p, string? sku = null, string? barcode = null, string name = "Café Molido", string price = "95.00", bool active = true, int? version = null) =>
        new(p.Id, version ?? p.Version, name, sku ?? p.Sku, barcode ?? p.Barcode, price, "H87", active);

    [Fact]
    public async Task DatosValidos_ActualizaEIncrementaLaVersion()
    {
        var product = Seed();

        var result = await Handler.HandleAsync(Command(product, active: false), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("Café Molido", result.Value.Name);
        Assert.Equal(9500, result.Value.PriceCents);
        Assert.False(result.Value.IsActive);
        Assert.Equal(product.Version + 1, result.Value.Version);
    }

    [Fact]
    public async Task MismasValidacionesQueElAlta()
    {
        var product = Seed();

        var result = await Handler.HandleAsync(Command(product, name: "", price: "12,50"), Ct);

        var error = Assert.IsType<ValidationFailed>(result.Error);
        Assert.Equal([ProductFields.Name, ProductFields.Price], error.Errors.Select(e => e.Field));
    }

    [Fact]
    public async Task ConservarElPropioSku_EsValido()
    {
        var product = Seed(barcode: "7501234567890");

        var result = await Handler.HandleAsync(Command(product), Ct);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task SkuDeOtroProducto_DevuelveDuplicate()
    {
        Seed(sku: "OTRO-1");
        var product = Seed(sku: "CAF-001");

        var result = await Handler.HandleAsync(Command(product, sku: "otro-1"), Ct);

        Assert.Equal(new Duplicate(ProductFields.Sku), result.Error);
    }

    [Fact]
    public async Task CodigoDeBarrasDeOtroProducto_DevuelveDuplicate()
    {
        Seed(sku: "OTRO-1", barcode: "7501234567890");
        var product = Seed(sku: "CAF-001");

        var result = await Handler.HandleAsync(Command(product, barcode: "7501234567890"), Ct);

        Assert.Equal(new Duplicate(ProductFields.Barcode), result.Error);
    }

    [Fact]
    public async Task ProductoInexistente_DevuelveNotFound()
    {
        var command = new UpdateProductCommand(Guid.CreateVersion7(), 1, "Nombre", "SKU1", null, "1", "H87", true);

        var result = await Handler.HandleAsync(command, Ct);

        Assert.IsType<NotFound>(result.Error);
    }

    [Fact]
    public async Task ProductoBorrado_DevuelveNotFound()
    {
        var product = Product.Create("Café", "CAF-001", null, Money.FromCents(100), "H87");
        product.Delete(DateTime.UtcNow);
        _repository.Seed(product);

        var result = await Handler.HandleAsync(Command(product), Ct);

        Assert.IsType<NotFound>(result.Error);
    }

    [Fact]
    public async Task VersionDesactualizada_DevuelveConflictSinGuardar()
    {
        var product = Seed();
        _repository.BumpVersion(product.Id);

        var result = await Handler.HandleAsync(Command(product, version: product.Version), Ct);

        Assert.IsType<Conflict>(result.Error);
        Assert.Equal("Café", _repository.All.Single().Name);
    }

    [Fact]
    public async Task ConflictoDetectadoAlGuardar_DevuelveConflict()
    {
        var product = Seed();
        _repository.NextOutcome = SaveOutcome.Conflict;

        var result = await Handler.HandleAsync(Command(product), Ct);

        Assert.IsType<Conflict>(result.Error);
    }
}
