using Pos.Application.Abstractions;
using Pos.Application.Inventory;
using Pos.Application.Products;
using Pos.Application.Products.UpdateProduct;
using Pos.Application.Tests.TestSupport;
using Pos.Domain.Common;
using Pos.Domain.Products;

namespace Pos.Application.Tests.Products;

public class UpdateProductInventoryTests
{
    private readonly InMemoryProductRepository _products = new();
    private readonly InMemoryInventoryRepository _inventory = new();
    private readonly FakeWriteTransactions _transactions = new();

    private UpdateProductHandler Handler => new(_products, new UpdateProductValidator(), _inventory, _transactions);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Product Seed(string unit = "KGM", bool tracks = true, string sku = "AZU-1") =>
        _products.Seed(Product.Create("Azúcar", sku, null, Money.FromCents(2500), unit, tracks, null));

    private static UpdateProductCommand Command(Product p, string? unit = null, bool? tracks = null, string? minimum = null) =>
        new(p.Id, p.Version, p.Name, p.Sku, p.Barcode, "25.00", unit ?? p.UnitCode, true, null, tracks ?? p.TracksInventory, minimum);

    [Fact]
    public async Task Without_movements_unit_and_tracking_can_change()
    {
        var product = Seed();

        var result = await Handler.HandleAsync(Command(product, unit: "H87", tracks: false), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("H87", result.Value.UnitCode);
        Assert.False(result.Value.TracksInventory);
        Assert.Equal(1, _transactions.Commits);
    }

    [Fact]
    public async Task With_movements_changing_the_unit_is_rejected()
    {
        var product = Seed();
        _inventory.SeedMovement(product.Id);

        var result = await Handler.HandleAsync(Command(product, unit: "H87"), Ct);

        var error = Assert.IsType<ValidationFailed>(result.Error);
        Assert.Equal(ProductFields.UnitCode, Assert.Single(error.Errors).Field);
        Assert.Equal(0, _transactions.Commits);
    }

    [Fact]
    public async Task With_movements_disabling_tracking_is_rejected_but_keeping_it_is_allowed()
    {
        var product = Seed();
        _inventory.SeedMovement(product.Id);

        var rejected = await Handler.HandleAsync(Command(product, tracks: false), Ct);
        var allowed = await Handler.HandleAsync(Command(product, minimum: "5"), Ct);

        var error = Assert.IsType<ValidationFailed>(rejected.Error);
        Assert.Equal(ProductFields.TracksInventory, Assert.Single(error.Errors).Field);
        Assert.True(allowed.IsSuccess);
        Assert.Equal(5000, allowed.Value.MinimumStockThousandths);
        Assert.True(allowed.Value.HasMovements);
    }

    [Fact]
    public async Task Minimum_stock_respects_the_unit_decimals()
    {
        var whole = Seed("H87");

        var result = await Handler.HandleAsync(Command(whole, minimum: "1.5"), Ct);

        var error = Assert.IsType<ValidationFailed>(result.Error);
        Assert.Equal(ProductFields.MinimumStock, Assert.Single(error.Errors).Field);
        Assert.Equal(InventoryMessages.NoDecimals("Pieza"), error.Errors[0].Message);

        var kilo = Seed("KGM", sku: "AZU-2");
        Assert.True((await Handler.HandleAsync(Command(kilo, minimum: "1.5"), Ct)).IsSuccess);
    }
}
