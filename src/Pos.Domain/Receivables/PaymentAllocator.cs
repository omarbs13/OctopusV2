using Pos.Domain.Common;

namespace Pos.Domain.Receivables;

/// <summary>Saldo de una cuenta pendiente con su orden FIFO: fecha de la venta e id.</summary>
public sealed record PendingBalance(Guid ReceivableId, DateTime CreatedAt, long BalanceCents);

/// <summary>Parte de un monto aplicada a una cuenta.</summary>
public sealed record Allocation(Guid ReceivableId, long AmountCents);

/// <summary>Reparto FIFO de un monto entre las cuentas pendientes de un cliente (014, FR-010, FR-011).</summary>
public static class PaymentAllocator
{
    /// <summary>
    /// Cubre por completo la cuenta más antigua (<c>CreatedAt</c>, <c>Id</c>) antes de pasar a la
    /// siguiente. Lanza si el monto es ≤ 0 o mayor que la suma de los saldos.
    /// </summary>
    public static IReadOnlyList<Allocation> Allocate(long amountCents, IEnumerable<PendingBalance> pending)
    {
        ArgumentNullException.ThrowIfNull(pending);
        var ordered = pending.Where(p => p.BalanceCents > 0).OrderBy(p => p.CreatedAt).ThenBy(p => p.ReceivableId).ToList();
        if (amountCents <= 0 || amountCents > ordered.Sum(p => p.BalanceCents))
        {
            throw new DomainException("El monto debe ser mayor que 0 y no exceder el saldo del cliente.");
        }

        var result = new List<Allocation>();
        var remaining = amountCents;
        foreach (var item in ordered)
        {
            if (remaining == 0)
            {
                break;
            }

            var applied = Math.Min(remaining, item.BalanceCents);
            result.Add(new Allocation(item.ReceivableId, applied));
            remaining -= applied;
        }

        return result;
    }
}
