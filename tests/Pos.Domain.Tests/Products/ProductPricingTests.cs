using Pos.Domain.Common;
using Pos.Domain.Products;
using Pos.Domain.Sales;

namespace Pos.Domain.Tests.Products;

public class ProductPricingTests
{
    private static Product Tile(long? sqm = 17503, long? box = 28705, long? packaging = 1640, string unit = "XBX", long? piece = null) =>
        Product.Create(
            new ProductDetails("Piedra", PackagingThousandths: packaging, PricePerSqmCents: sqm, PricePerBoxCents: box, PiecePriceCents: piece),
            "P-1",
            null,
            unit);

    [Fact]
    public void PriceFor_UsaLosPreciosCapturados()
    {
        var product = Tile();

        Assert.Equal(28705, product.PriceFor(SaleMode.Box));
        Assert.Equal(17503, product.PriceFor(SaleMode.SquareMeter));
        Assert.Null(product.PriceFor(SaleMode.Piece));
    }

    [Fact]
    public void PriceFor_DerivaCajaDeMetroCuadradoYViceversaConRedondeo()
    {
        var onlySqm = Tile(sqm: 19625, box: null, packaging: 1860);
        var onlyBox = Tile(sqm: null, box: 36503, packaging: 1860);

        Assert.Equal(36503, onlySqm.PriceFor(SaleMode.Box)); // 196.25 × 1.86 = 365.025
        Assert.Equal(19625, onlyBox.PriceFor(SaleMode.SquareMeter)); // 365.03 ÷ 1.86 = 196.2527
    }

    [Fact]
    public void PriceFor_SinEmbalajeNoSeDeriva()
    {
        var onlyBox = Tile(sqm: null, box: 28705, packaging: null);

        Assert.Null(onlyBox.PriceFor(SaleMode.SquareMeter));
        Assert.Equal([SaleMode.Box], onlyBox.AvailableModes);
    }

    [Fact]
    public void PriceFor_PiezaSoloUsaElPrecioPorPieza()
    {
        var withPiece = Tile(piece: 1500);

        Assert.Equal(1500, withPiece.PriceFor(SaleMode.Piece));
        Assert.Equal([SaleMode.Box, SaleMode.SquareMeter, SaleMode.Piece], withPiece.AvailableModes);
    }

    [Fact]
    public void ProductoQueNoEsLoseta_SoloSeVendePorPieza()
    {
        var product = Product.Create("Café", "CAF-1", null, Money.FromCents(8950), "H87");

        Assert.Equal(8950, product.PriceFor(SaleMode.Piece));
        Assert.Null(product.PriceFor(SaleMode.Box));
        Assert.Equal([SaleMode.Piece], product.AvailableModes);
    }

    [Fact]
    public void LosPreciosDeLosatasEnOtrosProductosSeIgnoran()
    {
        var product = Product.Create(
            new ProductDetails("Café", PackagingThousandths: 1000, PricePerSqmCents: 100, PricePerBoxCents: 100, PiecePriceCents: 500),
            "CAF-1",
            null,
            "H87");

        Assert.Null(product.PackagingThousandths);
        Assert.Null(product.PricePerSqmCents);
        Assert.Null(product.PricePerBoxCents);
        Assert.Equal(500, product.PiecePriceCents);
    }

    [Fact]
    public void Loseta_RequiereAlMenosXM2OXCaja()
    {
        Assert.Throws<DomainException>(() => Tile(sqm: null, box: null));
        Assert.Equal(17503, Tile(sqm: 17503, box: null).PricePerSqmCents);
        Assert.Equal(28705, Tile(sqm: null, box: 28705).PricePerBoxCents);
    }

    [Fact]
    public void NoLoseta_RequierePrecioPorPiezaMayorQueCero()
    {
        Assert.Throws<DomainException>(() => Product.Create(new ProductDetails("Café"), "CAF-1", null, "H87"));
        Assert.Throws<DomainException>(() => Product.Create(new ProductDetails("Café", PiecePriceCents: 0), "CAF-1", null, "H87"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Embalaje_CeroONegativoSeRechaza(long packaging)
    {
        Assert.Throws<DomainException>(() => Tile(packaging: packaging));
    }

    [Fact]
    public void Embalaje_SeGuardaEnMilesimas()
    {
        Assert.Equal(1800, Tile(packaging: 1800).PackagingThousandths);
        Assert.True(Product.IsValidPackaging(1800));
        Assert.False(Product.IsValidPackaging(0));
        Assert.False(Product.IsValidPackaging(-1));
    }

    [Fact]
    public void ModelEsObligatorioYElNombreSeDerivaDeModeloYMedidas()
    {
        Assert.Throws<DomainException>(() => Product.Create(new ProductDetails(" ", PiecePriceCents: 100), "A-1", null, "H87"));

        var product = Product.Create(new ProductDetails("  Sunwood Miel ", Measures: "18 X 60", PiecePriceCents: 100), "A-1", null, "H87");

        Assert.Equal("Sunwood Miel", product.Model);
        Assert.Equal("Sunwood Miel 18 X 60", product.Name);
        Assert.Equal("18X60", product.MeasuresKey);
    }

    [Fact]
    public void ToProductUnitThousandths_ConvierteM2ACajasConElEmbalaje()
    {
        var product = Tile(packaging: 1640);

        Assert.Equal(3000, product.ToProductUnitThousandths(SaleMode.SquareMeter, 4920));
        Assert.Equal(2000, product.ToProductUnitThousandths(SaleMode.Box, 2000));
        Assert.Null(Tile(packaging: null, box: 28705).ToProductUnitThousandths(SaleMode.SquareMeter, 4920));
    }

    [Fact]
    public void ProductoConInventarioSinEmbalaje_SoloOfreceLaModalidadDeSuUnidad()
    {
        var product = Product.Create(
            new ProductDetails("Piedra", PricePerSqmCents: 17503, PricePerBoxCents: 28705),
            "P-1",
            null,
            "XBX",
            tracksInventory: true);

        Assert.Equal([SaleMode.Box], product.AvailableModes);
    }
}
