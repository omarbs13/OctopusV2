using Pos.Domain.Common;
using Pos.Domain.Discounts;

namespace Pos.Domain.Tests.Discounts;

/// <summary>015, research §2–§3 y SC-001: cálculo al centavo y comparación exacta con el límite.</summary>
public sealed class DiscountMathTests
{
    [Fact]
    public void Amount_PorcentajeRedondeaMitadHaciaArriba()
    {
        // Historia 1, escenario 5: 3 × 33.33 = 99.99; 15 % = 14.9985 → 15.00; final 84.99.
        var discount = DiscountMath.Amount(9_999, DiscountValue.Percent(1500));

        Assert.Equal(1_500, discount);
        Assert.Equal(8_499, 9_999 - discount);
    }

    [Fact]
    public void Amount_RechazaMontoMayorQueLaBaseYDescuentoDeCeroCentavos()
    {
        Assert.Equal(10_000, DiscountMath.Amount(10_000, DiscountValue.Amount(10_000)));
        Assert.Throws<DomainException>(() => DiscountMath.Amount(10_000, DiscountValue.Amount(10_001)));

        // 1 % de $0.10 = 0.1 centavos → 0: se rechaza al aplicarlo.
        Assert.Throws<DomainException>(() => DiscountMath.Amount(10, DiscountValue.Percent(100)));
    }

    [Fact]
    public void ExceedsLimit_IgualAlLimiteNoLoSupera_UnCentavoMasSi()
    {
        // Límite 10 %: $15.00 sobre $150.00 es exactamente 10 % (quickstart, escenario 6).
        Assert.False(DiscountMath.ExceedsLimit(1_500, 15_000, 1_000));
        Assert.True(DiscountMath.ExceedsLimit(1_501, 15_000, 1_000));

        // Un porcentaje equivalente que se redondearía a 10.00 % pero lo supera: 1 001 sobre 10 009.
        Assert.True(DiscountMath.ExceedsLimit(1_001, 10_009, 1_000));
    }

    [Fact]
    public void EquivalentBasisPoints_RedondeaHaciaArribaParaQueLaAprobacionCubra()
    {
        Assert.Equal(1_500, DiscountMath.EquivalentBasisPoints(1_500, 10_000));
        Assert.Equal(1_001, DiscountMath.EquivalentBasisPoints(1_001, 10_009));
        Assert.Equal(10_000, DiscountMath.EquivalentBasisPoints(10_000, 10_000));
    }
}
