namespace Pos.Application.Audit.SearchAuditLog;

/// <summary>Filtros de la bitácora; el límite superior de fecha es exclusivo (UTC).</summary>
public sealed record SearchAuditLogQuery(
    DateTime? FromUtc,
    DateTime? ToUtcExclusive,
    Guid? UserId,
    string? Action,
    AuditEntityGroup? Entity = null,
    AuditRecordRef? Record = null,
    int Page = 1);
