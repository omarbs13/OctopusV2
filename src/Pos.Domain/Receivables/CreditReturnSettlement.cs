using Pos.Domain.Common;

namespace Pos.Domain.Receivables;

/// <summary>
/// Cómo se liquida la devolución de una venta a crédito (research §8): lo abonado de más sobre lo
/// devuelto (<c>ExcessCents</c>) se aplica FIFO a las otras cuentas del cliente y lo que sobra se
/// reintegra en efectivo.
/// </summary>
public sealed record CreditSettlementPlan(long ReturnedCents, long ExcessCents, IReadOnlyList<Allocation> Reapplied, long CashRefundCents)
{
    public long ReappliedCents => Reapplied.Sum(a => a.AmountCents);

    /// <summary>Cuánto baja la deuda total del cliente: lo devuelto menos lo que se reintegra en efectivo.</summary>
    public long ReducesBalanceCents => ReturnedCents - CashRefundCents;
}

public static class CreditReturnSettlement
{
    /// <param name="returnedCents">Monto devuelto o cancelado de la venta (R).</param>
    /// <param name="ownBalanceCents">Saldo actual de la cuenta de la venta.</param>
    /// <param name="otherPending">Las otras cuentas pendientes del cliente.</param>
    public static CreditSettlementPlan Settle(long returnedCents, long ownBalanceCents, IEnumerable<PendingBalance> otherPending)
    {
        ArgumentNullException.ThrowIfNull(otherPending);
        if (returnedCents <= 0)
        {
            throw new DomainException("El monto devuelto debe ser mayor que 0.");
        }

        var excess = Math.Max(0, returnedCents - Math.Max(0, ownBalanceCents));
        var others = otherPending.Where(p => p.BalanceCents > 0).ToList();
        var reapply = Math.Min(excess, others.Sum(p => p.BalanceCents));
        IReadOnlyList<Allocation> reapplied = reapply > 0 ? PaymentAllocator.Allocate(reapply, others) : [];
        return new CreditSettlementPlan(returnedCents, excess, reapplied, excess - reapply);
    }
}
