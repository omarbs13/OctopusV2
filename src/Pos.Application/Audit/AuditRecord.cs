using Pos.Domain.Audit;

namespace Pos.Application.Audit;

/// <summary>
/// Entrada de la bitácora con nombre legible del registro, motivo y cambios de campo (018). Los
/// eventos sin antes y después siguen usando la firma corta de <c>IAuditLog.Add</c>.
/// </summary>
public sealed record AuditRecord(
    string Action,
    string EntityType,
    Guid EntityId,
    string? EntityName = null,
    string? Details = null,
    string? Reason = null,
    IReadOnlyList<AuditFieldChange>? Changes = null,
    Guid? AuthorizedBy = null);
