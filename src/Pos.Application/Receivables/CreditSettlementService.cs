using Pos.Domain.Receivables;

namespace Pos.Application.Receivables;

/// <summary>Efecto de una devolución sobre el crédito del cliente (research §8).</summary>
public sealed record CreditSettlement(long ReducesBalanceCents, long ReappliedCents, long CashRefundCents);

/// <summary>
/// Punto único que ajusta las cuentas por cobrar por una cancelación o devolución de una venta a
/// crédito (014, FR-016): reduce el saldo de la venta (<c>RETURN</c>), libera lo abonado de más
/// (<c>EXCESS_OUT</c>), lo aplica FIFO a las otras cuentas pendientes del cliente (<c>EXCESS_IN</c>) y
/// calcula el reintegro en efectivo de lo que sobra. Trabaja dentro de la transacción del llamador y
/// no depende de la licencia de Crédito y clientes: es integridad de datos (research §11).
/// </summary>
public sealed class CreditSettlementService
{
    private readonly IReceivableRepository _receivables;

    public CreditSettlementService(IReceivableRepository receivables) => _receivables = receivables;

    /// <summary>Calcula sin escribir; nulo si la venta no es a crédito.</summary>
    public async Task<CreditSettlement?> PreviewAsync(Guid saleId, long returnedCents, CancellationToken cancellationToken)
    {
        var own = await _receivables.GetBySaleAsync(saleId, cancellationToken);
        if (own is null)
        {
            return null;
        }

        var plan = CreditReturnSettlement.Settle(returnedCents, own.BalanceCents, await OthersAsync(own, cancellationToken));
        return new CreditSettlement(plan.ReducesBalanceCents, plan.ReappliedCents, plan.CashRefundCents);
    }

    /// <summary>
    /// Aplica la devolución a las cuentas del cliente; <paramref name="closesSale"/> indica que la venta
    /// se canceló completa o quedó totalmente devuelta, y entonces su cuenta pasa a <c>CANCELLED</c>.
    /// <paramref name="originId"/> es la devolución que lo origina. Nulo si la venta no es a crédito.
    /// </summary>
    public async Task<CreditSettlement?> ApplyAsync(
        Guid saleId,
        Guid originId,
        long returnedCents,
        bool closesSale,
        CancellationToken cancellationToken)
    {
        var own = await _receivables.GetBySaleAsync(saleId, cancellationToken);
        if (own is null)
        {
            return null;
        }

        var others = (await _receivables.GetPendingAsync(own.CustomerId, cancellationToken)).Where(r => r.Id != own.Id).ToList();
        var plan = CreditReturnSettlement.Settle(
            returnedCents,
            own.BalanceCents,
            others.Select(r => new PendingBalance(r.Id, r.CreatedAt, r.BalanceCents)));

        own.ApplyReturn(originId, returnedCents, out _);
        foreach (var allocation in plan.Reapplied)
        {
            others.Single(r => r.Id == allocation.ReceivableId).ApplyExcess(originId, allocation.AmountCents);
        }

        if (closesSale)
        {
            own.Cancel();
        }

        return new CreditSettlement(plan.ReducesBalanceCents, plan.ReappliedCents, plan.CashRefundCents);
    }

    private async Task<IEnumerable<PendingBalance>> OthersAsync(Receivable own, CancellationToken cancellationToken) =>
        (await _receivables.GetPendingAsync(own.CustomerId, cancellationToken))
            .Where(r => r.Id != own.Id)
            .Select(r => new PendingBalance(r.Id, r.CreatedAt, r.BalanceCents));
}
