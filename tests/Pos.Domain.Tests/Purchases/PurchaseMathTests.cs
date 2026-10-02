using Pos.Domain.Common;
using Pos.Domain.Purchases;

namespace Pos.Domain.Tests.Purchases;

/// <summary>020, research §4: importes de línea, subtotal, total y límites.</summary>
public sealed class PurchaseMathTests
{
    [Fact]
    public void Ejemplo1_PiezasYKilos()
    {
        long[] lines = [PurchaseMath.LineAmount(5_000, 1_250), PurchaseMath.LineAmount(2_500, 4_000)];

        Assert.Equal([6_250, 10_000], lines);
        Assert.Equal(16_250, PurchaseMath.Subtotal(lines));
        Assert.Equal(18_850, PurchaseMath.Total(PurchaseMath.Subtotal(lines), 2_600));
    }

    [Fact]
    public void Ejemplo2_ConRedondeoPorLinea()
    {
        long[] lines = [PurchaseMath.LineAmount(3_000, 1_000), PurchaseMath.LineAmount(1_255, 2_000)];

        Assert.Equal([3_000, 2_510], lines);
        Assert.Equal(5_510, PurchaseMath.Subtotal(lines));
        Assert.Equal(6_392, PurchaseMath.Total(5_510, 882));
    }

    [Fact]
    public void Redondeo_MitadHaciaArriba_YSubtotalEsSumaDeRedondeados()
    {
        // 0.005 × $1.00 = 0.5 centavos → 1; 0.004 × $1.00 = 0.4 → 0.
        Assert.Equal(1, PurchaseMath.LineAmount(5, 100));
        Assert.Equal(0, PurchaseMath.LineAmount(4, 100));

        // Tres líneas de 0.005 × $1.00: la suma de redondeados (3) y no el redondeo de la suma (1.5 → 2).
        Assert.Equal(3, PurchaseMath.Subtotal([PurchaseMath.LineAmount(5, 100), PurchaseMath.LineAmount(5, 100), PurchaseMath.LineAmount(5, 100)]));
    }

    [Fact]
    public void ImportesSobreElMaximo_SeSenalan()
    {
        Assert.True(PurchaseMath.LineExceedsMaximum(2_000, Money.MaxCents));
        Assert.False(PurchaseMath.LineExceedsMaximum(1_000, Money.MaxCents));
        Assert.Throws<DomainException>(() => PurchaseMath.LineAmount(2_000, Money.MaxCents));

        Assert.Equal(PurchaseAmountLimit.Line, PurchaseMath.ExceededLimit([Money.MaxCents + 1], 0));
        Assert.Equal(PurchaseAmountLimit.Subtotal, PurchaseMath.ExceededLimit([Money.MaxCents, 1], 0));
        Assert.Equal(PurchaseAmountLimit.Total, PurchaseMath.ExceededLimit([Money.MaxCents], 1));
        Assert.Equal(PurchaseAmountLimit.None, PurchaseMath.ExceededLimit([Money.MaxCents - 1], 1));
    }
}
