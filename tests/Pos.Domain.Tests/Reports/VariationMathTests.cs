using Pos.Domain.Reports;

namespace Pos.Domain.Tests.Reports;

public class VariationMathTests
{
    [Fact]
    public void Variacion_CasoValido()
    {
        Assert.Equal(1_250, VariationMath.PercentBasisPoints(1_000, 1_125));
        Assert.Equal(-5_000, VariationMath.PercentBasisPoints(1_000, 500));
    }

    [Fact]
    public void PeriodoAnteriorEnCero_NoEsCalculable()
    {
        Assert.Null(VariationMath.PercentBasisPoints(0, 500));
    }
}
