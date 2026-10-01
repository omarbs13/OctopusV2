using Pos.Domain.Common;
using Pos.Domain.Returns;
using Pos.Domain.Sales;

namespace Pos.Application.Returns;

/// <summary>Monto de una devolución asignado a un pago de la venta original.</summary>
internal sealed record PaymentShare(SalePayment Payment, long AmountCents);

/// <summary>Lo que se devolvería: líneas con monto, total y reparto entre las formas de pago originales.</summary>
internal sealed record ReturnPlan(long TotalCents, IReadOnlyList<ReturnLineAmount> Lines, IReadOnlyList<PaymentShare> Shares)
{
    public long CashCents => Shares.Where(s => s.Payment.Method == PaymentMethod.Cash).Sum(s => s.AmountCents);

    /// <summary>Parte de una venta a crédito (014): la liquida <c>CreditSettlementService</c>, no la caja.</summary>
    public long OnAccountCents => Shares.Where(s => s.Payment.Method == PaymentMethod.OnAccount).Sum(s => s.AmountCents);
}

/// <summary>Calcula una devolución sin modificar la venta; la comparten la vista previa y el procesador.</summary>
internal static class ReturnPlanner
{
    /// <summary>Todo lo que aún se puede devolver de la venta.</summary>
    public static IReadOnlyList<ReturnLineRequest> AllAvailable(Sale sale) =>
        [.. sale.Lines.Where(l => l.AvailableToReturn > 0).Select(l => new ReturnLineRequest(l.Id, l.AvailableToReturn))];

    /// <exception cref="DomainException">Cantidades inválidas o monto mayor a lo pagado.</exception>
    public static ReturnPlan Build(Sale sale, IReadOnlyList<ReturnLineRequest> requests, IReadOnlyDictionary<Guid, long> returnedByPayment)
    {
        var lines = sale.CalculateReturn(requests);
        var total = lines.Sum(l => l.AmountCents);

        // Orden de captura: los ids son GUID v7, crecen con el orden en que se crearon los pagos.
        var payments = sale.Payments.OrderBy(p => p.Id).ToList();
        var remaining = payments
            .Select(p => Math.Max(0, p.AmountCents - returnedByPayment.GetValueOrDefault(p.Id)))
            .ToList();
        var allocation = ReturnMath.Allocate(total, remaining);

        var shares = payments
            .Select((p, i) => new PaymentShare(p, allocation[i]))
            .Where(s => s.AmountCents > 0)
            .ToList();
        return new ReturnPlan(total, lines, shares);
    }
}
