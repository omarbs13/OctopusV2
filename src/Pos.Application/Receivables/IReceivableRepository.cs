using Pos.Domain.Receivables;

namespace Pos.Application.Receivables;

/// <summary>
/// Persistencia de las cuentas por cobrar (014). Las cuentas se cargan con todas sus entradas y con
/// seguimiento de cambios; no hay <c>Remove</c>: una cuenta nunca se borra. Comparte la unidad de
/// trabajo con los demás repositorios.
/// </summary>
public interface IReceivableRepository
{
    Task<Receivable?> GetBySaleAsync(Guid saleId, CancellationToken cancellationToken);

    /// <summary>Cuentas <c>PENDING</c> del cliente en orden FIFO (<c>CreatedAt</c>, <c>Id</c>).</summary>
    Task<IReadOnlyList<Receivable>> GetPendingAsync(Guid customerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Receivable>> GetManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    void Add(Receivable receivable);

    /// <summary>Entradas <c>PAYMENT</c> y <c>PAYMENT_VOID</c> de un abono, sin seguimiento.</summary>
    Task<IReadOnlyList<ReceivableEntry>> GetEntriesByPaymentAsync(Guid paymentId, CancellationToken cancellationToken);

    /// <summary>Indica si alguna de las cuentas tuvo una devolución (<c>RETURN</c> o <c>EXCESS_OUT</c>) después de <paramref name="afterUtc"/> (research §7).</summary>
    Task<bool> HasReturnAfterAsync(IReadOnlyCollection<Guid> receivableIds, DateTime afterUtc, CancellationToken cancellationToken);

    /// <summary>Fecha de la venta de la cuenta pendiente más antigua del cliente, o nula si no debe nada.</summary>
    Task<DateTime?> GetOldestPendingDateAsync(Guid customerId, CancellationToken cancellationToken);

    /// <summary>Ventas a crédito del cliente, de la más reciente a la más antigua; <c>DaysOverdue</c> viene en 0.</summary>
    Task<ReceivablePage> ListByCustomerAsync(Guid customerId, bool onlyPending, int page, int pageSize, CancellationToken cancellationToken);
}
