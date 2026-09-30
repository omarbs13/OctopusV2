using Pos.Domain.Common;
using Pos.Domain.Inventory;
using Pos.Domain.Products;

namespace Pos.Domain.Tests.Inventory;

public class ProductStockTests
{
    private static readonly UnitOfMeasure Piece = UnitOfMeasure.Piece;
    private static readonly UnitOfMeasure Kilo = UnitOfMeasure.All.Single(u => u.Code == "KGM");

    private static Quantity Q(string text, int places = 3) => Quantity.Parse(text, places).Value!.Value;

    private static InventoryMovement Record(
        ProductStock stock,
        MovementType type,
        string quantity,
        UnitOfMeasure? unit = null,
        string? reason = null,
        bool active = true,
        bool tracks = true)
    {
        unit ??= Piece;
        return stock.Record(type, Q(quantity, unit.DecimalPlaces), unit, active, tracks, reason, null);
    }

    [Fact]
    public void Sequence_of_movements_computes_resulting_stock()
    {
        var stock = ProductStock.Start(Guid.NewGuid());

        var m1 = Record(stock, MovementType.Initial, "10");
        var m2 = Record(stock, MovementType.Receipt, "5");
        var m3 = Record(stock, MovementType.AdjustOut, "3", reason: "merma");

        Assert.Equal([10_000, 15_000, 12_000], new[] { m1, m2, m3 }.Select(m => m.ResultingStock.Thousandths));
        Assert.Equal([1, 2, 3], new[] { m1, m2, m3 }.Select(m => m.Sequence));
        Assert.Equal(12_000, stock.OnHand.Thousandths);
        Assert.Equal(3, stock.MovementCount);
    }

    [Fact]
    public void AdjustOut_to_exactly_zero_is_allowed_but_one_thousandth_more_is_rejected()
    {
        var stock = ProductStock.Start(Guid.NewGuid());
        Record(stock, MovementType.Initial, "3.000", Kilo);

        Assert.Throws<DomainException>(() => Record(stock, MovementType.AdjustOut, "3.001", Kilo, "x"));
        Assert.Equal(3000, stock.OnHand.Thousandths);

        Record(stock, MovementType.AdjustOut, "3.000", Kilo, "x");
        Assert.Equal(0, stock.OnHand.Thousandths);
    }

    [Fact]
    public void Adjustments_require_a_reason_but_receipts_do_not()
    {
        var stock = ProductStock.Start(Guid.NewGuid());
        Record(stock, MovementType.Initial, "10");

        Assert.Throws<DomainException>(() => Record(stock, MovementType.AdjustIn, "1", reason: "  "));
        Record(stock, MovementType.Receipt, "1");
    }

    [Fact]
    public void Initial_is_rejected_when_the_product_already_has_movements()
    {
        var stock = ProductStock.Start(Guid.NewGuid());
        Record(stock, MovementType.Receipt, "1");

        Assert.Throws<DomainException>(() => Record(stock, MovementType.Initial, "5"));
    }

    [Fact]
    public void Product_that_does_not_track_or_is_inactive_is_rejected()
    {
        var stock = ProductStock.Start(Guid.NewGuid());

        Assert.Throws<DomainException>(() => Record(stock, MovementType.Receipt, "1", tracks: false));
        Assert.Throws<DomainException>(() => Record(stock, MovementType.Receipt, "1", active: false));
    }

    [Fact]
    public void Decimals_beyond_the_unit_are_rejected()
    {
        var stock = ProductStock.Start(Guid.NewGuid());

        Assert.Throws<DomainException>(() =>
            stock.Record(MovementType.Receipt, Quantity.FromThousandths(1500), Piece, true, true, null, null));
        stock.Record(MovementType.Receipt, Quantity.FromThousandths(1500), Kilo, true, true, null, null);
    }

    [Fact]
    public void Zero_quantity_and_stock_above_the_maximum_are_rejected()
    {
        var stock = ProductStock.Start(Guid.NewGuid());

        Assert.Throws<DomainException>(() =>
            stock.Record(MovementType.Receipt, Quantity.Zero, Kilo, true, true, null, null));

        var big = Quantity.FromThousandths(Quantity.MaxCaptureThousandths);
        for (var i = 0; i < 100; i++)
        {
            stock.Record(MovementType.Receipt, big, Kilo, true, true, null, null);
        }

        Assert.Throws<DomainException>(() => stock.Record(MovementType.Receipt, big, Kilo, true, true, null, null));
    }
}
