using Pos.Domain.Common;
using Pos.Domain.Inventory;
using Pos.Domain.Products;
using Pos.Domain.Sales;

namespace Pos.Domain.Tests.Sales;

public class TileSaleTests
{
    private static Product Tile(string unit = "XBX", long? piece = null, bool tracks = false) =>
        Product.Create(
            new ProductDetails("Piedra", PackagingThousandths: 1640, PricePerSqmCents: 17503, PricePerBoxCents: 28705, PiecePriceCents: piece),
            "P-1",
            null,
            unit,
            tracks);

    private static CartProduct ToCartProduct(Product product)
    {
        var mode = product.InitialMode;
        var unit = UnitOfMeasure.Find(product.UnitCodeFor(mode))!;
        return new CartProduct(
            product.Id,
            product.Name,
            product.Sku,
            unit.Code,
            unit.DecimalPlaces,
            product.TracksInventory,
            Money.FromCents(product.PriceFor(mode)!.Value),
            Tile: new TileSaleInfo(product.AvailableModes, product.PricePerSqmCents, product.PricePerBoxCents, product.PiecePriceCents, product.PackagingThousandths));
    }

    [Fact]
    public void TresCajasA28705_Son86115()
    {
        var cart = new Cart();
        cart.Add(ToCartProduct(Tile()), Quantity.FromThousandths(3000));

        Assert.Equal(86115, cart.Total.Cents);
    }

    [Fact]
    public void CuatroPuntoNoventaYDosM2A17503_Son86115()
    {
        var product = Tile();
        var cart = new Cart();
        cart.Add(ToCartProduct(product), Quantity.FromThousandths(1000));

        cart.SetMode(product.Id, SaleMode.SquareMeter);
        cart.SetQuantity(product.Id, Quantity.FromThousandths(4920));

        Assert.Equal("MTK", cart.Lines[0].UnitCode);
        Assert.Equal(17503, cart.Lines[0].UnitPrice.Cents);
        Assert.Equal(86115, cart.Total.Cents);
    }

    [Fact]
    public void CambiarDeModalidad_AjustaLaCantidadALosDecimalesDeLaUnidad()
    {
        var product = Tile(piece: 1500);
        var cart = new Cart();
        cart.Add(ToCartProduct(product), Quantity.FromThousandths(2500));

        cart.SetMode(product.Id, SaleMode.Piece);

        Assert.Equal("H87", cart.Lines[0].UnitCode);
        Assert.Equal(3000, cart.Lines[0].Quantity.Thousandths);
        Assert.Equal(1500, cart.Lines[0].UnitPrice.Cents);

        cart.SetMode(product.Id, SaleMode.Box);
        Assert.Equal(3000, cart.Lines[0].Quantity.Thousandths);
        Assert.Equal(28705, cart.Lines[0].UnitPrice.Cents);
    }

    [Fact]
    public void ModalidadSinPrecio_NoEstaDisponibleYLaLineaNoCambia()
    {
        var product = Tile();
        var cart = new Cart();
        cart.Add(ToCartProduct(product), Quantity.FromThousandths(2000));

        Assert.Throws<DomainException>(() => cart.SetMode(product.Id, SaleMode.Piece));

        Assert.Equal("XBX", cart.Lines[0].UnitCode);
        Assert.Equal(2000, cart.Lines[0].Quantity.Thousandths);
    }

    [Fact]
    public void ProductoSinLoseta_NoCambiaDeModalidad()
    {
        var cart = new Cart();
        var id = Guid.CreateVersion7();
        cart.Add(new CartProduct(id, "Café", "CAF-1", "H87", 0, false, Money.FromCents(100)));

        Assert.Throws<DomainException>(() => cart.SetMode(id, SaleMode.Box));
    }

    [Fact]
    public void PiezaSinPrecioPorPieza_NoSeVende()
    {
        var product = Tile(piece: null);

        Assert.DoesNotContain(SaleMode.Piece, product.AvailableModes);
        Assert.Null(product.PriceFor(SaleMode.Piece));
    }

    [Theory]
    [InlineData(0, 4920, 3000)]
    [InlineData(0, 1640, 1000)]
    [InlineData(1640, 3280, 2000)]
    public void SaleStockRestore_LaLineaVendidaEnM2RegresaCajasProporcionales(long already, long returning, long expected)
    {
        // La línea se vendió en m² (4.920) y salieron 3.000 cajas: cada 1.640 m² son 1.000 cajas.
        Assert.Equal(expected, SaleStockRestore.Quantity(3000, 4920, already, returning));
    }

    [Fact]
    public void SaleStockRestore_EnPartesSumaExactamenteElMovimiento()
    {
        // 7 piezas = 2.333 cajas (3 piezas por caja); devolver de a una pieza no deja residuo.
        const long movement = 2333;
        const long line = 7000;
        long total = 0;
        for (var i = 0; i < 7; i++)
        {
            total += SaleStockRestore.Quantity(movement, line, i * 1000L, 1000);
        }

        Assert.Equal(movement, total);
    }

    [Fact]
    public void SaleStockRestore_MismaUnidadRegresaLaCantidadDevuelta()
    {
        Assert.Equal(400, SaleStockRestore.Quantity(5000, 5000, 1000, 400));
        Assert.Equal(5000, SaleStockRestore.Quantity(5000, 5000, 0, 5000));
    }
}
