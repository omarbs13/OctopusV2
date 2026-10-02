using Pos.Application.Products;
using Pos.Domain.Purchases;

namespace Pos.Application.Purchases;

/// <summary>
/// Persistencia del agregado Compra (020). Comparte la unidad de trabajo con inventario y bitácora. No ofrece
/// <c>Remove</c>: una compra no se borra, se anula (FR-017).
/// </summary>
public interface IPurchaseRepository
{
    /// <summary>Compra con sus líneas y seguimiento de cambios, o nula.</summary>
    Task<Purchase?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Compra vigente del proveedor con esa clave de factura, sin seguimiento, o nula (research §5).</summary>
    Task<Purchase?> FindActiveByInvoiceAsync(Guid supplierId, string invoiceKey, CancellationToken cancellationToken);

    void Add(Purchase purchase);

    /// <summary>Detalle sin seguimiento con los nombres de quien registró y de quien anuló, o nulo.</summary>
    Task<PurchaseDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Guarda la unidad de trabajo. <see cref="SaveStatus.Duplicate"/> con <see cref="PurchaseFields.InvoiceNumber"/> si
    /// el índice de factura vigente se violó; <see cref="SaveStatus.Conflict"/> por concurrencia u otro índice.
    /// </summary>
    Task<SaveOutcome> SaveChangesAsync(CancellationToken cancellationToken);
}
