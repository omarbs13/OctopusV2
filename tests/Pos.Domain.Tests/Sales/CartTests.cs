using Pos.Domain.Common;
using Pos.Domain.Sales;

namespace Pos.Domain.Tests.Sales;

public class CartTests
{
    private static Quantity Q(string text) => Quantity.Parse(text, 3, allowZero: true).Value!.Value;

    private static Money M(string text) => Money.Parse(text).Value!.Value;

    private static CartProduct Piece(string price = "10.00", UnavailableReason? reason = null) =>
        new(Guid.NewGuid(), "Refresco", "REF-1", "H87", 0, true, M(price), reason);

    private static CartProduct Kilo(string price = "20.00") =>
        new(Guid.NewGuid(), "Jitomate", "JIT-1", "KGM", 3, false, M(price));

    [Fact]
    public void Adding_the_same_product_increments_its_line()
    {
        var cart = new Cart();
        var product = Piece();

        cart.Add(product);
        cart.Add(product);

        Assert.Single(cart.Lines);
        Assert.Equal(2000, cart.Lines[0].Quantity.Thousandths);
        Assert.Equal(2000, cart.Total.Cents);
    }

    [Fact]
    public void Decimal_quantity_on_a_piece_is_rejected_and_keeps_the_previous_value()
    {
        var cart = new Cart();
        var product = Piece();
        cart.Add(product);

        Assert.Throws<DomainException>(() => cart.SetQuantity(product.ProductId, Q("1.5")));

        Assert.Equal(1000, cart.Lines[0].Quantity.Thousandths);
    }

    [Fact]
    public void Decimal_quantity_on_a_weighed_product_is_accepted()
    {
        var cart = new Cart();
        var product = Kilo();
        cart.Add(product);

        cart.SetQuantity(product.ProductId, Q("1.250"));

        Assert.Equal(2500, cart.Total.Cents);
    }

    [Theory]
    [InlineData("0")]
    public void Zero_quantity_is_rejected(string quantity)
    {
        var cart = new Cart();
        var product = Piece();
        cart.Add(product);

        Assert.Throws<DomainException>(() => cart.SetQuantity(product.ProductId, Q(quantity)));
        Assert.Equal(1000, cart.Lines[0].Quantity.Thousandths);
    }

    [Fact]
    public void Inactive_product_is_not_added()
    {
        var cart = new Cart();

        Assert.Throws<DomainException>(() => cart.Add(Piece(reason: UnavailableReason.Inactive)));
        Assert.Empty(cart.Lines);
    }

    [Fact]
    public void Total_above_the_maximum_is_rejected_and_keeps_the_previous_state()
    {
        var cart = new Cart();
        var product = Piece("999999.99");
        cart.Add(product);

        Assert.Throws<DomainException>(() => cart.Add(product));

        Assert.Equal(1000, cart.Lines[0].Quantity.Thousandths);
    }

    [Fact]
    public void CanCheckout_requires_lines_a_positive_total_and_available_products()
    {
        var cart = new Cart();
        Assert.False(cart.CanCheckout);

        var free = Piece("0.00");
        cart.Add(free);
        Assert.False(cart.CanCheckout);

        var product = Piece();
        cart.Add(product);
        Assert.True(cart.CanCheckout);

        cart.ApplyCurrentPrices([new CartPriceUpdate(product.ProductId, M("10.00"), UnavailableReason.Deleted)]);
        Assert.False(cart.CanCheckout);
        Assert.True(cart.Lines.Single(l => l.ProductId == product.ProductId).IsUnavailable);
    }

    [Fact]
    public void ApplyCurrentPrices_updates_the_total()
    {
        var cart = new Cart();
        var product = Piece("10.00");
        cart.Add(product, Q("3"));

        cart.ApplyCurrentPrices([new CartPriceUpdate(product.ProductId, M("12.50"), null)]);

        Assert.Equal(3750, cart.Total.Cents);
    }

    [Fact]
    public void Clear_starts_a_new_draft()
    {
        var cart = new Cart();
        var draft = cart.DraftId;
        cart.Add(Piece());

        cart.Clear();

        Assert.Empty(cart.Lines);
        Assert.NotEqual(draft, cart.DraftId);
    }

    [Fact]
    public void Restore_rebuilds_the_lines_with_the_draft_id()
    {
        var product = Piece();
        var draftId = Guid.CreateVersion7();
        var line = new CartLine(product.ProductId, product.Name, product.Sku, product.UnitCode, 0, true, product.UnitPrice, Q("2"));

        var cart = Cart.Restore(draftId, [line]);

        Assert.Equal(draftId, cart.DraftId);
        Assert.Equal(2000, cart.Total.Cents);
    }
}
