using Pos.Domain.Common;

namespace Pos.Domain.Audit;

/// <summary>
/// Entrada inmutable de la bitácora de auditoría (Principio IX). <c>CreatedAt</c> y <c>CreatedBy</c>
/// los asigna la persistencia.
/// </summary>
public sealed class AuditEntry
{
    public const int ActionMaxLength = 40;
    public const int EntityTypeMaxLength = 40;
    public const int DetailsMaxLength = 500;

    private AuditEntry()
    {
        Action = string.Empty;
        EntityType = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Action { get; private set; }

    public string EntityType { get; private set; }

    public Guid EntityId { get; private set; }

    public string? Details { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public static AuditEntry Create(string action, string entityType, Guid entityId, string? details)
    {
        if (string.IsNullOrWhiteSpace(action) || action.Length > ActionMaxLength
            || string.IsNullOrWhiteSpace(entityType) || entityType.Length > EntityTypeMaxLength)
        {
            throw new DomainException("La entrada de auditoría no es válida.");
        }

        var text = string.IsNullOrWhiteSpace(details) ? null : details.Trim();
        if (text is { Length: > DetailsMaxLength })
        {
            text = text[..DetailsMaxLength];
        }

        return new AuditEntry
        {
            Id = Guid.CreateVersion7(),
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = text,
        };
    }
}
