using Pos.Domain.Reports;

namespace Pos.Domain.Tests.Reports;

public class CashDifferenceRuleTests
{
    [Fact]
    public void FaltanteDeSeisPorCiento_EsAlertaConUmbralDeCinco()
    {
        var basisPoints = CashDifferenceRule.PercentBasisPoints(-6_000, 100_000);

        Assert.Equal(-600, basisPoints);
        Assert.True(CashDifferenceRule.IsAlert(basisPoints, CashDifferenceRule.DefaultThresholdBasisPoints));
    }

    [Fact]
    public void ExactamenteElUmbral_NoEsAlerta()
    {
        Assert.False(CashDifferenceRule.IsAlert(500, 500));
        Assert.False(CashDifferenceRule.IsAlert(-500, 500));
        Assert.True(CashDifferenceRule.IsAlert(501, 500));
    }

    [Fact]
    public void EsperadoCero_NoEsCalculableNiAlerta()
    {
        var basisPoints = CashDifferenceRule.PercentBasisPoints(1_000, 0);

        Assert.Null(basisPoints);
        Assert.False(CashDifferenceRule.IsAlert(basisPoints, 500));
    }
}
