using Pos.Domain.Audit;
using Pos.Domain.Common;

namespace Pos.Domain.Tests.Audit;

/// <summary>018: un cambio de campo de la bitácora conserva sus valores y nunca es un "cambio" sin diferencia.</summary>
public sealed class AuditFieldChangeTests
{
    [Fact]
    public void CambioValido_ConservaCampoAntesYDespues()
    {
        var change = new AuditFieldChange("Precio", "$25.00", "$28.50");

        Assert.Equal("Precio", change.Field);
        Assert.Equal("$25.00", change.Before);
        Assert.Equal("$28.50", change.After);
    }

    [Theory]
    [InlineData("$25.00", "$25.00")]
    [InlineData(null, null)]
    public void AntesYDespuesIguales_SeRechaza(string? before, string? after) =>
        Assert.Throws<DomainException>(() => new AuditFieldChange("Precio", before, after));
}
