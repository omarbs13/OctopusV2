using Pos.Application.Audit;

namespace Pos.Application.Abstractions;

/// <summary>
/// Bitácora de auditoría de operaciones sensibles (Principio IX). <c>Add</c> no guarda: la
/// entrada viaja en la transacción del caso de uso que la generó.
/// </summary>
public interface IAuditLog
{
    /// <summary>Agrega la entrada; <c>authorizedBy</c> es el administrador que autorizó la operación, si la hubo (007, FR-014).</summary>
    void Add(string action, string entityType, Guid entityId, string? details, Guid? authorizedBy = null);

    /// <summary>
    /// Agrega la entrada con nombre del registro, motivo y cambios de campo (018). Con una lista de
    /// cambios vacía en una modificación, el llamador no debe invocarlo (FR-002).
    /// </summary>
    void Add(AuditRecord record);

    /// <summary>
    /// Persiste lo agregado con <c>Add</c> cuando no hay otra escritura que lo lleve (por
    /// ejemplo, la apertura del cajón sin venta).
    /// </summary>
    Task SaveAsync(CancellationToken cancellationToken);
}
