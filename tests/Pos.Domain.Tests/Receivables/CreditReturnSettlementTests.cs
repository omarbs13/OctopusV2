using Pos.Domain.Receivables;

namespace Pos.Domain.Tests.Receivables;

/// <summary>014, research §8: reduce, reaplica FIFO y reintegra lo que sobra.</summary>
public sealed class CreditReturnSettlementTests
{
    private static readonly DateTime Day1 = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void SinAbonos_SoloReduceElSaldo()
    {
        var plan = CreditReturnSettlement.Settle(returnedCents: 50_000, ownBalanceCents: 50_000, []);

        Assert.Equal(0, plan.ExcessCents);
        Assert.Empty(plan.Reapplied);
        Assert.Equal(0, plan.CashRefundCents);
        Assert.Equal(50_000, plan.ReducesBalanceCents);
    }

    [Fact]
    public void ConAbonosDeMas_ReaplicaFifoALasOtrasCuentas()
    {
        // Quickstart escenario 9: venta de $500 con $200 abonados (saldo $300) y otra de $200 pendiente.
        var other = new PendingBalance(Guid.CreateVersion7(), Day1, 20_000);

        var plan = CreditReturnSettlement.Settle(returnedCents: 50_000, ownBalanceCents: 30_000, [other]);

        Assert.Equal(20_000, plan.ExcessCents);
        Assert.Equal([new Allocation(other.ReceivableId, 20_000)], plan.Reapplied);
        Assert.Equal(0, plan.CashRefundCents);
        Assert.Equal(50_000, plan.ReducesBalanceCents);
    }

    [Fact]
    public void SinOtrasDeudas_ReintegraElExcedenteEnEfectivo()
    {
        var small = new PendingBalance(Guid.CreateVersion7(), Day1, 5_000);

        var plan = CreditReturnSettlement.Settle(returnedCents: 50_000, ownBalanceCents: 30_000, [small]);

        Assert.Equal(20_000, plan.ExcessCents);
        Assert.Equal(5_000, plan.ReappliedCents);
        Assert.Equal(15_000, plan.CashRefundCents);
        Assert.Equal(35_000, plan.ReducesBalanceCents);
    }
}
