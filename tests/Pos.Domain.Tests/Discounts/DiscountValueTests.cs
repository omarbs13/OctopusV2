using Pos.Domain.Common;
using Pos.Domain.Discounts;

namespace Pos.Domain.Tests.Discounts;

/// <summary>015, research §1: el valor capturado se guarda entero y nunca se redondea.</summary>
public sealed class DiscountValueTests
{
    [Theory]
    [InlineData("10", 1000)]
    [InlineData("12.5", 1250)]
    [InlineData(" 0.01 ", 1)]
    [InlineData("100", 10_000)]
    [InlineData("100.00", 10_000)]
    public void Parse_PorcentajeEnPuntosBase(string text, long expected)
    {
        var value = DiscountValue.Parse(DiscountMode.Percent, text);

        Assert.Equal(DiscountMode.Percent, value.Mode);
        Assert.Equal(expected, value.Raw);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("100.01")]
    [InlineData("12.345")]
    [InlineData("-5")]
    [InlineData("abc")]
    public void Parse_RechazaPorcentajeFueraDeRangoOConMasDeDosDecimales(string text) =>
        Assert.Throws<DomainException>(() => DiscountValue.Parse(DiscountMode.Percent, text));

    [Fact]
    public void Parse_MontoEnCentavos_YRechazaCeroOTresDecimales()
    {
        Assert.Equal(1500, DiscountValue.Parse(DiscountMode.Amount, "15").Raw);
        Assert.Throws<DomainException>(() => DiscountValue.Parse(DiscountMode.Amount, "0"));
        Assert.Throws<DomainException>(() => DiscountValue.Parse(DiscountMode.Amount, "1.005"));
    }

    [Fact]
    public void ToString_MuestraPorcentajeOMonto()
    {
        Assert.Equal("10%", DiscountValue.Percent(1000).ToString());
        Assert.Equal("12.5%", DiscountValue.Percent(1250).ToString());
        Assert.Equal("$15.00", DiscountValue.Amount(1500).ToString());
    }
}
