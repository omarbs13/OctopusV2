using Pos.Domain.Products;

namespace Pos.Domain.Tests.Products;

public class ProductPricingSuggestionTests
{
    [Fact]
    public void SuggestBoxPrice_XM2PorEmbalaje_RedondeaMitadHaciaArriba()
    {
        Assert.Equal(36503, ProductPricing.SuggestBoxPrice(19625, 1860)); // 365.025 → 365.03
    }

    [Fact]
    public void SuggestSquareMeterPrice_XCajaEntreEmbalajeConRedondeo()
    {
        Assert.Equal(19625, ProductPricing.SuggestSquareMeterPrice(36503, 1860)); // 196.2527 → 196.25
    }

    [Theory]
    [InlineData(null, 1860L)]
    [InlineData(19625L, null)]
    [InlineData(19625L, 0L)]
    [InlineData(0L, 1860L)]
    public void SinDatosNoSugiere(long? price, long? packaging)
    {
        Assert.Null(ProductPricing.SuggestBoxPrice(price, packaging));
        Assert.Null(ProductPricing.SuggestSquareMeterPrice(price, packaging));
    }

    [Theory]
    [InlineData(10200, false)] // justo en 2 %
    [InlineData(10201, true)]
    [InlineData(9800, false)]
    [InlineData(9799, true)]
    [InlineData(10000, false)]
    public void ExceedsTolerance_SoloPorEncimaDel2PorCiento(long box, bool expected)
    {
        // X M2 = $100.00 y Embalaje = 1.000 → esperado $100.00 (10000 centavos).
        Assert.Equal(expected, ProductPricing.ExceedsTolerance(box, 10000, 1000));
    }
}
