using Pos.Domain.Reports;

namespace Pos.Domain.Tests.Reports;

/// <summary>016: porcentaje del total en puntos base (research §10).</summary>
public class ShareMathTests
{
    [Fact]
    public void Participacion_MitadHaciaArriba()
    {
        Assert.Equal(5_000, ShareMath.BasisPoints(100_000, 200_000));
        Assert.Equal(3_333, ShareMath.BasisPoints(1, 3));
        Assert.Equal(6_667, ShareMath.BasisPoints(2, 3));
    }

    [Fact]
    public void TotalEnCero_DevuelveCero() => Assert.Equal(0, ShareMath.BasisPoints(0, 0));
}
