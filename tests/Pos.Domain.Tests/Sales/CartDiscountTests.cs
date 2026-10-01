using Pos.Domain.Common;
using Pos.Domain.Discounts;
using Pos.Domain.Sales;

namespace Pos.Domain.Tests.Sales;

/// <summary>015: descuentos de línea, de venta y de cupón en la venta en curso (SC-001).</summary>
public sealed class CartDiscountTests
{
    private static Money M(string text) => Money.Parse(text).Value!.Value;

    private static Quantity Q(long thousandths) => Quantity.FromThousandths(thousandths);

    private static CartProduct Piece(string price) =>
        new(Guid.NewGuid(), "Producto", "SKU-" + price, "H87", 0, false, M(price));

    private static Cart CartWith(params (CartProduct Product, long Thousandths)[] lines)
    {
        var cart = new Cart();
        foreach (var (product, thousandths) in lines)
        {
            cart.Add(product, Q(thousandths));
        }

        return cart;
    }

    // --- Historia 1: descuento por línea ---

    [Fact]
    public void LineDiscount_PorcentajeYMonto_SobreElImporteDeLaLinea()
    {
        // Historia 1, escenario 1: 2 × 50.00 al 10 % → 90.00.
        var a = Piece("50.00");
        var cart = CartWith((a, 2000));

        cart.SetLineDiscount(a.ProductId, new LineDiscount(DiscountValue.Percent(1000)));

        Assert.Equal(10_000, cart.Lines[0].Amount.Cents);
        Assert.Equal(9_000, cart.Lines[0].NetBeforeOrder.Cents);
        Assert.Equal(9_000, cart.Total.Cents);
        Assert.Equal(1_000, cart.TotalDiscount.Cents);

        // Un monto mayor que el importe se rechaza y la línea conserva su descuento anterior.
        Assert.Throws<DomainException>(() => cart.SetLineDiscount(a.ProductId, new LineDiscount(DiscountValue.Amount(10_001))));
        Assert.Equal(9_000, cart.Total.Cents);

        // Quitarlo regresa al importe original (escenario 6).
        cart.SetLineDiscount(a.ProductId, null);
        Assert.Equal(10_000, cart.Total.Cents);
    }

    [Fact]
    public void SetQuantity_RecalculaElPorcentaje_YRechazaUnMontoQueYaNoCabe()
    {
        var a = Piece("50.00");
        var b = Piece("33.33");
        var cart = CartWith((a, 2000), (b, 3000));
        cart.SetLineDiscount(a.ProductId, new LineDiscount(DiscountValue.Percent(1000)));
        cart.SetLineDiscount(b.ProductId, new LineDiscount(DiscountValue.Amount(6_000)));

        cart.SetQuantity(a.ProductId, Q(3000));
        Assert.Equal(13_500, cart.Lines[0].NetBeforeOrder.Cents);

        // b: 3 × 33.33 = 99.99 con $60.00; bajar a 1 (33.33) haría el monto mayor que el importe.
        Assert.Throws<DomainException>(() => cart.SetQuantity(b.ProductId, Q(1000)));
        Assert.Equal(3000, cart.Lines[1].Quantity.Thousandths);
    }

    [Fact]
    public void DescuentoDel100PorCiento_DejaTotalCeroYSePuedeCobrar()
    {
        var a = Piece("10.00");
        var cart = CartWith((a, 1000));

        cart.SetLineDiscount(a.ProductId, new LineDiscount(DiscountValue.Percent(10_000), Guid.CreateVersion7()));

        Assert.Equal(0, cart.Total.Cents);
        Assert.True(cart.CanCheckout);
    }

    // --- Historia 2: descuento en la venta total ---

    [Fact]
    public void OrderDiscount_PorcentajeSobreElSubtotalYaDescontado_YSeRecalculaAlCambiarLineas()
    {
        var a = Piece("100.00");
        var b = Piece("100.00");
        var cart = CartWith((a, 1000), (b, 1000));
        cart.SetLineDiscount(a.ProductId, new LineDiscount(DiscountValue.Amount(2_000)));

        // Escenario 3: 10 % sobre 180.00 (no sobre 200.00).
        cart.SetOrderDiscount(new OrderDiscount.Manual(DiscountValue.Percent(1000)));
        Assert.Equal(18_000, cart.Subtotal.Cents);
        Assert.Equal(1_800, cart.OrderDiscountAmount.Cents);
        Assert.Equal(16_200, cart.Total.Cents);

        // Escenario 4: al quitar una línea se recalcula sobre el nuevo subtotal.
        cart.Remove(b.ProductId);
        Assert.Equal(800, cart.OrderDiscountAmount.Cents);
        Assert.Equal(7_200, cart.Total.Cents);
    }

    [Fact]
    public void OrderDiscount_MontoQueSuperaElSubtotal_SeRechazaAlAplicarYSeRetiraSiElSubtotalBaja()
    {
        // Quickstart, escenario 7: subtotal 150.00 con $60.00; al quitar C queda 50.00 < 60.00.
        var a = Piece("50.00");
        var c = Piece("100.00");
        var cart = CartWith((c, 1000), (a, 1000));
        Assert.Throws<DomainException>(() => cart.SetOrderDiscount(new OrderDiscount.Manual(DiscountValue.Amount(15_001))));

        cart.SetOrderDiscount(new OrderDiscount.Manual(DiscountValue.Amount(6_000)));
        Assert.Equal(9_000, cart.Total.Cents);

        cart.Remove(c.ProductId);

        Assert.Null(cart.OrderDiscount);
        Assert.IsType<OrderDiscount.Manual>(cart.LastRemovedOrderDiscount);
        Assert.Equal(5_000, cart.Total.Cents);
    }

    [Fact]
    public void Allocation_RepartePorRestoMayorYLaSumaDeNetosEsElTotal()
    {
        var lines = new[] { Piece("33.33"), Piece("33.33"), Piece("33.34") };
        var cart = CartWith([.. lines.Select(p => (p, 1000L))]);
        cart.SetOrderDiscount(new OrderDiscount.Manual(DiscountValue.Amount(1_000)));

        var allocation = cart.Allocation();

        Assert.Equal(1_000, allocation.Sum());
        Assert.Equal([333, 333, 334], allocation);
        Assert.Equal(cart.Total.Cents, cart.Lines.Select((l, i) => l.NetBeforeOrder.Cents - allocation[i]).Sum());
    }

    // --- Historia 3: cupones ---

    [Fact]
    public void Cupon_DeMontoMayorQueElSubtotal_SeLimitaAlSubtotal_YReemplazaAlManual()
    {
        var a = Piece("50.00");
        var cart = CartWith((a, 1000));
        cart.SetOrderDiscount(new OrderDiscount.Manual(DiscountValue.Percent(500)));

        // Un solo descuento de venta: el cupón reemplaza al manual (FR-013).
        cart.SetOrderDiscount(new OrderDiscount.CouponApplied(Guid.CreateVersion7(), "VALE100", DiscountValue.Amount(10_000)));

        Assert.IsType<OrderDiscount.CouponApplied>(cart.OrderDiscount);
        Assert.Equal(5_000, cart.OrderDiscountAmount.Cents);
        Assert.Equal(0, cart.Total.Cents);
    }
}
