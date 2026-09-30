namespace Pos.Application.Abstractions;

/// <summary>
/// Bitácora de auditoría de operaciones sensibles (Principio IX). <see cref="Add"/> no guarda: la
/// entrada viaja en la transacción del caso de uso que la generó.
/// </summary>
public interface IAuditLog
{
    void Add(string action, string entityType, Guid entityId, string? details);
}
