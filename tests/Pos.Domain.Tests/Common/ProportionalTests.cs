using Pos.Domain.Common;

namespace Pos.Domain.Tests.Common;

/// <summary>015, FR-004: el reparto del descuento de venta suma exactamente el descuento.</summary>
public sealed class ProportionalTests
{
    [Fact]
    public void Allocate_SumaExactaPorRestoMayor()
    {
        // $10.00 de descuento sobre líneas de $33.33, $33.33 y $33.34.
        var parts = Proportional.Allocate(1_000, [3_333, 3_333, 3_334]);

        Assert.Equal(1_000, parts.Sum());
        Assert.Equal([333, 333, 334], parts);
    }

    [Fact]
    public void Allocate_EmpateSeAsignaPorOrdenDeCaptura_YRechazaExcederLaBase()
    {
        Assert.Equal([1, 0, 0], Proportional.Allocate(1, [100, 100, 100]));
        Assert.Throws<DomainException>(() => Proportional.Allocate(301, [100, 100, 100]));
    }
}
