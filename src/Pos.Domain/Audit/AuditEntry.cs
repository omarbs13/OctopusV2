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
    public const int EntityNameMaxLength = 200;
    public const int DetailsMaxLength = 500;
    public const int ReasonMaxLength = 250;

    private readonly List<AuditFieldChange> _changes = [];

    private AuditEntry()
    {
        Action = string.Empty;
        EntityType = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Action { get; private set; }

    public string EntityType { get; private set; }

    public Guid EntityId { get; private set; }

    /// <summary>Nombre legible del registro en ese momento ("Venta V-000123"); nulo en las entradas anteriores a 018.</summary>
    public string? EntityName { get; private set; }

    public string? Details { get; private set; }

    /// <summary>Motivo capturado por el usuario (cancelación, devolución, cajón).</summary>
    public string? Reason { get; private set; }

    /// <summary>Cambios de campo con su valor anterior y nuevo, en orden; vacía en los eventos sin cambios (018).</summary>
    public IReadOnlyList<AuditFieldChange> Changes => _changes;

    public DateTime CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    /// <summary>Administrador que autorizó la operación, si la hubo (FR-014).</summary>
    public Guid? AuthorizedBy { get; private set; }

    public static AuditEntry Create(
        string action,
        string entityType,
        Guid entityId,
        string? details,
        Guid? authorizedBy = null,
        string? entityName = null,
        string? reason = null,
        IEnumerable<AuditFieldChange>? changes = null)
    {
        if (string.IsNullOrWhiteSpace(action) || action.Length > ActionMaxLength
            || string.IsNullOrWhiteSpace(entityType) || entityType.Length > EntityTypeMaxLength)
        {
            throw new DomainException("La entrada de auditoría no es válida.");
        }

        var entry = new AuditEntry
        {
            Id = Guid.CreateVersion7(),
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            EntityName = Limit(entityName, EntityNameMaxLength),
            Details = Limit(details, DetailsMaxLength),
            Reason = Limit(reason, ReasonMaxLength),
            AuthorizedBy = authorizedBy,
        };
        if (changes is not null)
        {
            entry._changes.AddRange(changes);
        }

        return entry;
    }

    private static string? Limit(string? value, int maxLength)
    {
        var text = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return text is { Length: var length } && length > maxLength ? text[..maxLength] : text;
    }
}
