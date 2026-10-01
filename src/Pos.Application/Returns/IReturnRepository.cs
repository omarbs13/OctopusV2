using Pos.Application.Products;
using Pos.Domain.Returns;

namespace Pos.Application.Returns;

/// <summary>
/// Persistencia de devoluciones. No ofrece <c>Update</c> ni <c>Remove</c>: los registros son
/// inmutables salvo la reversa de un reintegro de tarjeta (FR-013, FR-018). Comparte la unidad de
/// trabajo con los demás repositorios.
/// </summary>
public interface IReturnRepository
{
    /// <summary><c>MAX(Number) + 1</c>; se llama dentro de la transacción de escritura.</summary>
    Task<long> NextNumberAsync(CancellationToken cancellationToken);

    void Add(SaleReturn saleReturn);

    /// <summary>Reintegro con seguimiento de cambios, o nulo.</summary>
    Task<SaleReturnRefund?> GetRefundAsync(Guid refundId, CancellationToken cancellationToken);

    /// <summary>Lo ya reintegrado de cada pago de la venta (id del pago → centavos).</summary>
    Task<IReadOnlyDictionary<Guid, long>> GetReturnedByPaymentAsync(Guid saleId, CancellationToken cancellationToken);

    Task<ReversalPage> SearchReversalsAsync(ReversalSearch search, CancellationToken cancellationToken);

    /// <summary>Folio de la devolución del reintegro, para la bitácora; nulo si no existe.</summary>
    Task<(string ReturnFolio, string SaleFolio, Guid SaleId)?> DescribeRefundAsync(Guid refundId, CancellationToken cancellationToken);

    Task<SaveOutcome> SaveChangesAsync(CancellationToken cancellationToken);
}
