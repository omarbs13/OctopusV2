using Pos.Domain.Audit;

namespace Pos.Application.Audit;

/// <summary>
/// Filtros de la bitácora; el límite superior de fecha es exclusivo (UTC). <c>UserId</c> es el usuario
/// involucrado: autor, autorizador o afectado. <c>Record</c> pide el historial de un registro (018, research §9).
/// </summary>
public sealed record AuditFilter(
    DateTime? FromUtc,
    DateTime? ToUtcExclusive,
    Guid? UserId,
    string? Action,
    AuditEntityGroup? Entity,
    AuditRecordRef? Record);

/// <summary>Registro cuyo historial se consulta.</summary>
public sealed record AuditRecordRef(string EntityType, Guid EntityId);

public sealed record AuditSearch(AuditFilter Filter, int Page, int PageSize);

public sealed record AuditRow(
    Guid Id,
    DateTime CreatedAtUtc,
    string Action,
    string EntityType,
    Guid EntityId,
    string? EntityName,
    string UserName,
    string? AuthorizedByName,
    string? Reason,
    string? Details,
    IReadOnlyList<AuditFieldChange> Changes);

public sealed record AuditPage(IReadOnlyList<AuditRow> Items, long TotalCount, int Page, int PageSize)
{
    public const int DefaultPageSize = 100;

    public int TotalPages => TotalCount <= 0 ? 1 : (int)((TotalCount + PageSize - 1) / PageSize);
}
