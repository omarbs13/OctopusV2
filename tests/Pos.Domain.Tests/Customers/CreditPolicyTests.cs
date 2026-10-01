using Pos.Domain.Customers;

namespace Pos.Domain.Tests.Customers;

/// <summary>014, FR-006: <c>saldo + venta ≤ límite</c>.</summary>
public sealed class CreditPolicyTests
{
    [Fact]
    public void SaldoMasVentaIgualAlLimite_Pasa() =>
        Assert.True(CreditPolicy.Check(balanceCents: 70_000, limitCents: 100_000, saleCents: 30_000).IsWithin);

    [Fact]
    public void UnCentavoMas_ExcedePorUno()
    {
        var check = CreditPolicy.Check(balanceCents: 70_000, limitCents: 100_000, saleCents: 30_001);

        Assert.True(check.IsExceeded);
        Assert.Equal(CreditCheck.Exceeded(1), check);
    }
}
