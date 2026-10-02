using Pos.Application.Products;
using Pos.Domain.Inventory;

namespace Pos.Application.Inventory;

/// <summary>Producto que puede estar en alerta: existencia actual y umbrales, en milésimas (022).</summary>
public sealed record StockAlertCandidate(Guid ProductId, long OnHandThousandths, long? MinimumThousandths, long? ReorderPointThousandths);

/// <summary>Lecturas y registros de la revisión de alertas de existencia (022, contracts/application-ports.md).</summary>
public interface IStockAlertStore
{
    /// <summary>
    /// Productos activos, no borrados, que controlan inventario y tienen al menos un umbral; existencia
    /// actual (0 sin fila de existencia).
    /// </summary>
    Task<IReadOnlyList<StockAlertCandidate>> GetCandidatesAsync(CancellationToken cancellationToken);

    /// <summary>Niveles ya notificados al usuario en el día local indicado, por producto.</summary>
    Task<IReadOnlyList<(Guid ProductId, StockAlertLevel Level)>> GetAcknowledgedAsync(
        Guid userId, DateOnly localDate, CancellationToken cancellationToken);

    void Add(StockAlertAcknowledgement acknowledgement);

    /// <summary>Borra, dentro de la transacción en curso, las filas con día anterior al indicado (de todos los usuarios).</summary>
    Task PurgeBeforeAsync(DateOnly localDate, CancellationToken cancellationToken);

    /// <summary>Guarda los registros. <see cref="SaveOutcome.Conflict"/> si viola el índice único.</summary>
    Task<SaveOutcome> SaveChangesAsync(CancellationToken cancellationToken);
}
