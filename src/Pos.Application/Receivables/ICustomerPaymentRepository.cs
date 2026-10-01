using Pos.Application.Products;
using Pos.Domain.Receivables;

namespace Pos.Application.Receivables;

/// <summary>
/// Persistencia de los abonos (014). No ofrece <c>Remove</c>: un abono solo se anula (FR-014).
/// Comparte la unidad de trabajo con los demás repositorios.
/// </summary>
public interface ICustomerPaymentRepository
{
    /// <summary><c>MAX(Number) + 1</c>; se llama dentro de la transacción de escritura.</summary>
    Task<long> NextNumberAsync(CancellationToken cancellationToken);

    /// <summary>Abono registrado con la clave de idempotencia, sin seguimiento, o nulo.</summary>
    Task<CustomerPayment?> FindByRequestAsync(Guid requestId, CancellationToken cancellationToken);

    /// <summary>Abono con seguimiento de cambios, o nulo.</summary>
    Task<CustomerPayment?> GetAsync(Guid id, CancellationToken cancellationToken);

    void Add(CustomerPayment payment);

    /// <summary>Abonos del cliente, del más reciente al más antiguo, con los anulados.</summary>
    Task<CustomerPaymentPage> ListByCustomerAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>Datos del recibo, o nulo si el abono no existe.</summary>
    Task<CustomerPaymentReceiptData?> GetReceiptDataAsync(Guid paymentId, CancellationToken cancellationToken);

    /// <summary>
    /// Guarda la unidad de trabajo. <see cref="SaveStatus.Duplicate"/> con <c>Number</c> o
    /// <c>RequestId</c> si se viola su índice único; <see cref="SaveStatus.Conflict"/> en otro caso.
    /// </summary>
    Task<SaveOutcome> SaveChangesAsync(CancellationToken cancellationToken);
}
